namespace NovaUI.Desktop.Platforms.Linux;

/// <summary>Per-window swapchain; copies CPU-rasterized frames into the presentable image.</summary>
internal sealed unsafe class VulkanPresenter : IDisposable
{
    private const uint FormatB8G8R8A8Unorm = 44;
    private const uint FormatB8G8R8A8Srgb = 50;
    private const uint LayoutTransferDst = 7;
    private const uint LayoutPresentSrc = 1000001002;
    private const uint StageTransfer = 0x1000;

    private readonly VulkanDevice _vk;
    private readonly nint _surface;
    private readonly nint _commandPool;
    private readonly nint _commandBuffer;
    private readonly nint _acquired;
    private readonly nint _rendered;

    private nint _swapchain;
    private nint[] _images = [];
    private uint _width, _height;

    private nint _buffer, _memory;
    private void* _mapped;
    private ulong _capacity;

    public VulkanPresenter(VulkanDevice vk, nint surface)
    {
        _vk = vk;
        _surface = surface;

        var poolInfo = new VkCommandPoolCreateInfo { sType = 39, flags = 2, queueFamilyIndex = vk.QueueFamily };
        nint pool;
        Vk.Check(Vk.vkCreateCommandPool(vk.Device, &poolInfo, 0, &pool), nameof(Vk.vkCreateCommandPool));
        _commandPool = pool;

        var allocInfo = new VkCommandBufferAllocateInfo { sType = 40, commandPool = pool, commandBufferCount = 1 };
        nint cb;
        Vk.Check(Vk.vkAllocateCommandBuffers(vk.Device, &allocInfo, &cb), nameof(Vk.vkAllocateCommandBuffers));
        _commandBuffer = cb;

        var semInfo = new VkSemaphoreCreateInfo { sType = 9 };
        nint a, r;
        Vk.Check(Vk.vkCreateSemaphore(vk.Device, &semInfo, 0, &a), nameof(Vk.vkCreateSemaphore));
        Vk.Check(Vk.vkCreateSemaphore(vk.Device, &semInfo, 0, &r), nameof(Vk.vkCreateSemaphore));
        _acquired = a;
        _rendered = r;
    }

    public bool Present(nint pixels, int width, int height, int rowBytes, int windowWidth, int windowHeight)
    {
        VkSurfaceCapabilities caps;
        Vk.Check(Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(_vk.PhysicalDevice, _surface, &caps), "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

        var extentW = caps.currentWidth;
        var extentH = caps.currentHeight;
        if (extentW == uint.MaxValue)
        {
            extentW = Math.Clamp((uint)windowWidth, caps.minWidth, caps.maxWidth);
            extentH = Math.Clamp((uint)windowHeight, caps.minHeight, caps.maxHeight);
        }

        if (extentW == 0 || extentH == 0)
            return true;

        if (_swapchain == 0 || extentW != _width || extentH != _height)
            CreateSwapchain(caps, extentW, extentH);

        var size = (ulong)rowBytes * (ulong)height;
        EnsureStaging(size);
        Buffer.MemoryCopy((void*)pixels, _mapped, (long)_capacity, (long)size);

        uint index;
        var result = Vk.vkAcquireNextImageKHR(_vk.Device, _swapchain, ulong.MaxValue, _acquired, 0, &index);
        if (result == Vk.OutOfDate)
        {
            DestroySwapchain();
            return false;
        }

        Vk.Check(result, nameof(Vk.vkAcquireNextImageKHR));

        Record(_images[index], (uint)(rowBytes / 4), (uint)height, Math.Min((uint)width, _width), Math.Min((uint)height, _height));

        var cb = _commandBuffer;
        var waitSemaphore = _acquired;
        var signalSemaphore = _rendered;
        var waitStage = StageTransfer;
        var submit = new VkSubmitInfo
        {
            sType = 4,
            waitSemaphoreCount = 1, pWaitSemaphores = &waitSemaphore, pWaitDstStageMask = &waitStage,
            commandBufferCount = 1, pCommandBuffers = &cb,
            signalSemaphoreCount = 1, pSignalSemaphores = &signalSemaphore,
        };
        Vk.Check(Vk.vkQueueSubmit(_vk.Queue, 1, &submit, 0), nameof(Vk.vkQueueSubmit));

        var swapchain = _swapchain;
        var present = new VkPresentInfo
        {
            sType = 1000001001,
            waitSemaphoreCount = 1, pWaitSemaphores = &signalSemaphore,
            swapchainCount = 1, pSwapchains = &swapchain, pImageIndices = &index,
        };
        var presentResult = Vk.vkQueuePresentKHR(_vk.Queue, &present);
        Vk.vkQueueWaitIdle(_vk.Queue);

        if (presentResult is Vk.OutOfDate or Vk.Suboptimal)
        {
            DestroySwapchain();
            return presentResult == Vk.Suboptimal;
        }

        Vk.Check(presentResult, nameof(Vk.vkQueuePresentKHR));
        return true;
    }

    private void Record(nint image, uint bufferRowLength, uint bufferHeight, uint copyWidth, uint copyHeight)
    {
        Vk.Check(Vk.vkResetCommandBuffer(_commandBuffer, 0), nameof(Vk.vkResetCommandBuffer));
        var begin = new VkCommandBufferBeginInfo { sType = 42, flags = 1 };
        Vk.Check(Vk.vkBeginCommandBuffer(_commandBuffer, &begin), nameof(Vk.vkBeginCommandBuffer));

        var toTransfer = Barrier(image, 0, 0x1000, 0, LayoutTransferDst);
        Vk.vkCmdPipelineBarrier(_commandBuffer, 0x1, StageTransfer, 0, 0, 0, 0, 0, 1, &toTransfer);

        var region = new VkBufferImageCopy
        {
            bufferRowLength = bufferRowLength, bufferImageHeight = bufferHeight,
            aspectMask = 1, layerCount = 1,
            width = copyWidth, height = copyHeight, depth = 1,
        };
        Vk.vkCmdCopyBufferToImage(_commandBuffer, _buffer, image, LayoutTransferDst, 1, &region);

        var toPresent = Barrier(image, 0x1000, 0, LayoutTransferDst, LayoutPresentSrc);
        Vk.vkCmdPipelineBarrier(_commandBuffer, StageTransfer, 0x2000, 0, 0, 0, 0, 0, 1, &toPresent);

        Vk.Check(Vk.vkEndCommandBuffer(_commandBuffer), nameof(Vk.vkEndCommandBuffer));
    }

    private static VkImageMemoryBarrier Barrier(nint image, uint srcAccess, uint dstAccess, uint oldLayout, uint newLayout) => new()
    {
        sType = 45,
        srcAccessMask = srcAccess, dstAccessMask = dstAccess,
        oldLayout = oldLayout, newLayout = newLayout,
        srcQueueFamilyIndex = uint.MaxValue, dstQueueFamilyIndex = uint.MaxValue,
        image = image,
        aspectMask = 1, levelCount = 1, layerCount = 1,
    };

    private void CreateSwapchain(VkSurfaceCapabilities caps, uint width, uint height)
    {
        Vk.vkDeviceWaitIdle(_vk.Device);

        uint formatCount = 0;
        Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(_vk.PhysicalDevice, _surface, &formatCount, null);
        var formats = new VkSurfaceFormat[formatCount];
        fixed (VkSurfaceFormat* pFormats = formats)
            Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(_vk.PhysicalDevice, _surface, &formatCount, pFormats);

        var chosen = formats.FirstOrDefault(f => f.format == FormatB8G8R8A8Unorm);
        if (chosen.format == 0)
            chosen = formats.FirstOrDefault(f => f.format == FormatB8G8R8A8Srgb);
        if (chosen.format == 0)
            chosen = formats[0];

        var alpha = (caps.supportedCompositeAlpha & 1) != 0 ? 1u : caps.supportedCompositeAlpha & (~caps.supportedCompositeAlpha + 1);
        var imageCount = caps.minImageCount + 1;
        if (caps.maxImageCount > 0)
            imageCount = Math.Min(imageCount, caps.maxImageCount);

        var old = _swapchain;
        var info = new VkSwapchainCreateInfo
        {
            sType = 1000001000,
            surface = _surface,
            minImageCount = imageCount,
            imageFormat = chosen.format, imageColorSpace = chosen.colorSpace,
            width = width, height = height,
            imageArrayLayers = 1,
            imageUsage = 2, // TRANSFER_DST
            preTransform = caps.currentTransform,
            compositeAlpha = alpha,
            presentMode = 2, // FIFO: always available, vsynced
            clipped = 1,
            oldSwapchain = old,
        };
        nint swapchain;
        Vk.Check(Vk.vkCreateSwapchainKHR(_vk.Device, &info, 0, &swapchain), nameof(Vk.vkCreateSwapchainKHR));
        if (old != 0)
            Vk.vkDestroySwapchainKHR(_vk.Device, old, 0);

        uint count = 0;
        Vk.vkGetSwapchainImagesKHR(_vk.Device, swapchain, &count, null);
        _images = new nint[count];
        fixed (nint* pImages = _images)
            Vk.vkGetSwapchainImagesKHR(_vk.Device, swapchain, &count, pImages);

        _swapchain = swapchain;
        _width = width;
        _height = height;
    }

    private void DestroySwapchain()
    {
        if (_swapchain == 0)
            return;

        Vk.vkDeviceWaitIdle(_vk.Device);
        Vk.vkDestroySwapchainKHR(_vk.Device, _swapchain, 0);
        _swapchain = 0;
        _images = [];
    }

    private void EnsureStaging(ulong size)
    {
        if (_buffer != 0 && size <= _capacity)
            return;

        FreeStaging();

        var bufferInfo = new VkBufferCreateInfo { sType = 12, size = size, usage = 1 };
        nint buffer;
        Vk.Check(Vk.vkCreateBuffer(_vk.Device, &bufferInfo, 0, &buffer), nameof(Vk.vkCreateBuffer));

        VkMemoryRequirements req;
        Vk.vkGetBufferMemoryRequirements(_vk.Device, buffer, &req);

        var allocInfo = new VkMemoryAllocateInfo
        {
            sType = 5,
            allocationSize = req.size,
            memoryTypeIndex = _vk.FindHostVisibleMemoryType(req.memoryTypeBits),
        };
        nint memory;
        Vk.Check(Vk.vkAllocateMemory(_vk.Device, &allocInfo, 0, &memory), nameof(Vk.vkAllocateMemory));
        Vk.Check(Vk.vkBindBufferMemory(_vk.Device, buffer, memory, 0), nameof(Vk.vkBindBufferMemory));

        void* mapped;
        Vk.Check(Vk.vkMapMemory(_vk.Device, memory, 0, size, 0, &mapped), nameof(Vk.vkMapMemory));

        _buffer = buffer;
        _memory = memory;
        _mapped = mapped;
        _capacity = size;
    }

    private void FreeStaging()
    {
        if (_buffer == 0)
            return;

        Vk.vkDeviceWaitIdle(_vk.Device);
        Vk.vkDestroyBuffer(_vk.Device, _buffer, 0);
        Vk.vkFreeMemory(_vk.Device, _memory, 0);
        _buffer = _memory = 0;
        _mapped = null;
        _capacity = 0;
    }

    public void Dispose()
    {
        Vk.vkDeviceWaitIdle(_vk.Device);
        FreeStaging();
        DestroySwapchain();
        Vk.vkDestroySemaphore(_vk.Device, _acquired, 0);
        Vk.vkDestroySemaphore(_vk.Device, _rendered, 0);
        Vk.vkDestroyCommandPool(_vk.Device, _commandPool, 0);
        _vk.DestroySurface(_surface);
    }
}
