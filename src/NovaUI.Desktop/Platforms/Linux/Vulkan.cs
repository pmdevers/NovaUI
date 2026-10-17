using System.Runtime.InteropServices;

namespace NovaUI.Desktop.Platforms.Linux;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkInstanceCreateInfo
{
    public uint sType; public nint pNext; public uint flags; public nint pApplicationInfo;
    public uint enabledLayerCount; public byte** ppEnabledLayerNames;
    public uint enabledExtensionCount; public byte** ppEnabledExtensionNames;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkXlibSurfaceCreateInfo
{
    public uint sType; public nint pNext; public uint flags; public nint dpy; public nint window;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkQueueFamilyProperties
{
    public uint queueFlags, queueCount, timestampValidBits, granularityW, granularityH, granularityD;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDeviceQueueCreateInfo
{
    public uint sType; public nint pNext; public uint flags; public uint queueFamilyIndex; public uint queueCount;
    public float* pQueuePriorities;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkDeviceCreateInfo
{
    public uint sType; public nint pNext; public uint flags;
    public uint queueCreateInfoCount; public VkDeviceQueueCreateInfo* pQueueCreateInfos;
    public uint enabledLayerCount; public byte** ppEnabledLayerNames;
    public uint enabledExtensionCount; public byte** ppEnabledExtensionNames;
    public nint pEnabledFeatures;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSurfaceCapabilities
{
    public uint minImageCount, maxImageCount;
    public uint currentWidth, currentHeight, minWidth, minHeight, maxWidth, maxHeight;
    public uint maxImageArrayLayers, supportedTransforms, currentTransform, supportedCompositeAlpha, supportedUsageFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSurfaceFormat { public uint format, colorSpace; }

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkSwapchainCreateInfo
{
    public uint sType; public nint pNext; public uint flags; public nint surface;
    public uint minImageCount, imageFormat, imageColorSpace, width, height, imageArrayLayers, imageUsage, imageSharingMode;
    public uint queueFamilyIndexCount; public uint* pQueueFamilyIndices;
    public uint preTransform, compositeAlpha, presentMode, clipped;
    public nint oldSwapchain;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkSemaphoreCreateInfo { public uint sType; public nint pNext; public uint flags; }

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkBufferCreateInfo
{
    public uint sType; public nint pNext; public uint flags; public ulong size; public uint usage, sharingMode;
    public uint queueFamilyIndexCount; public uint* pQueueFamilyIndices;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkMemoryRequirements { public ulong size, alignment; public uint memoryTypeBits; }

[StructLayout(LayoutKind.Sequential)]
internal struct VkMemoryAllocateInfo { public uint sType; public nint pNext; public ulong allocationSize; public uint memoryTypeIndex; }

[StructLayout(LayoutKind.Sequential)]
internal struct VkCommandPoolCreateInfo { public uint sType; public nint pNext; public uint flags, queueFamilyIndex; }

[StructLayout(LayoutKind.Sequential)]
internal struct VkCommandBufferAllocateInfo { public uint sType; public nint pNext; public nint commandPool; public uint level, commandBufferCount; }

[StructLayout(LayoutKind.Sequential)]
internal struct VkCommandBufferBeginInfo { public uint sType; public nint pNext; public uint flags; public nint pInheritanceInfo; }

[StructLayout(LayoutKind.Sequential)]
internal struct VkImageMemoryBarrier
{
    public uint sType; public nint pNext; public uint srcAccessMask, dstAccessMask, oldLayout, newLayout, srcQueueFamilyIndex, dstQueueFamilyIndex;
    public nint image;
    public uint aspectMask, baseMipLevel, levelCount, baseArrayLayer, layerCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VkBufferImageCopy
{
    public ulong bufferOffset; public uint bufferRowLength, bufferImageHeight;
    public uint aspectMask, mipLevel, baseArrayLayer, layerCount;
    public int offsetX, offsetY, offsetZ;
    public uint width, height, depth;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkSubmitInfo
{
    public uint sType; public nint pNext;
    public uint waitSemaphoreCount; public nint* pWaitSemaphores; public uint* pWaitDstStageMask;
    public uint commandBufferCount; public nint* pCommandBuffers;
    public uint signalSemaphoreCount; public nint* pSignalSemaphores;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VkPresentInfo
{
    public uint sType; public nint pNext;
    public uint waitSemaphoreCount; public nint* pWaitSemaphores;
    public uint swapchainCount; public nint* pSwapchains; public uint* pImageIndices; public int* pResults;
}

/// <summary>Minimal Vulkan loader bindings (handles are pointer-sized on 64-bit).</summary>
internal static unsafe class Vk
{
    private const string Lib = "libvulkan.so.1";

    public const int Success = 0;
    public const int Suboptimal = 1000001003;
    public const int OutOfDate = -1000001004;

    [DllImport(Lib)] public static extern int vkCreateInstance(VkInstanceCreateInfo* info, nint alloc, nint* instance);
    [DllImport(Lib)] public static extern void vkDestroyInstance(nint instance, nint alloc);
    [DllImport(Lib)] public static extern int vkCreateXlibSurfaceKHR(nint instance, VkXlibSurfaceCreateInfo* info, nint alloc, nint* surface);
    [DllImport(Lib)] public static extern void vkDestroySurfaceKHR(nint instance, nint surface, nint alloc);
    [DllImport(Lib)] public static extern int vkEnumeratePhysicalDevices(nint instance, uint* count, nint* devices);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceQueueFamilyProperties(nint device, uint* count, VkQueueFamilyProperties* props);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceSupportKHR(nint device, uint family, nint surface, uint* supported);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceCapabilitiesKHR(nint device, nint surface, VkSurfaceCapabilities* caps);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceFormatsKHR(nint device, nint surface, uint* count, VkSurfaceFormat* formats);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceMemoryProperties(nint device, byte* props);
    [DllImport(Lib)] public static extern int vkCreateDevice(nint physical, VkDeviceCreateInfo* info, nint alloc, nint* device);
    [DllImport(Lib)] public static extern void vkDestroyDevice(nint device, nint alloc);
    [DllImport(Lib)] public static extern void vkGetDeviceQueue(nint device, uint family, uint index, nint* queue);
    [DllImport(Lib)] public static extern int vkCreateSwapchainKHR(nint device, VkSwapchainCreateInfo* info, nint alloc, nint* swapchain);
    [DllImport(Lib)] public static extern void vkDestroySwapchainKHR(nint device, nint swapchain, nint alloc);
    [DllImport(Lib)] public static extern int vkGetSwapchainImagesKHR(nint device, nint swapchain, uint* count, nint* images);
    [DllImport(Lib)] public static extern int vkAcquireNextImageKHR(nint device, nint swapchain, ulong timeout, nint semaphore, nint fence, uint* index);
    [DllImport(Lib)] public static extern int vkQueuePresentKHR(nint queue, VkPresentInfo* info);
    [DllImport(Lib)] public static extern int vkQueueSubmit(nint queue, uint count, VkSubmitInfo* submits, nint fence);
    [DllImport(Lib)] public static extern int vkQueueWaitIdle(nint queue);
    [DllImport(Lib)] public static extern int vkDeviceWaitIdle(nint device);
    [DllImport(Lib)] public static extern int vkCreateSemaphore(nint device, VkSemaphoreCreateInfo* info, nint alloc, nint* semaphore);
    [DllImport(Lib)] public static extern void vkDestroySemaphore(nint device, nint semaphore, nint alloc);
    [DllImport(Lib)] public static extern int vkCreateBuffer(nint device, VkBufferCreateInfo* info, nint alloc, nint* buffer);
    [DllImport(Lib)] public static extern void vkDestroyBuffer(nint device, nint buffer, nint alloc);
    [DllImport(Lib)] public static extern void vkGetBufferMemoryRequirements(nint device, nint buffer, VkMemoryRequirements* req);
    [DllImport(Lib)] public static extern int vkAllocateMemory(nint device, VkMemoryAllocateInfo* info, nint alloc, nint* memory);
    [DllImport(Lib)] public static extern void vkFreeMemory(nint device, nint memory, nint alloc);
    [DllImport(Lib)] public static extern int vkBindBufferMemory(nint device, nint buffer, nint memory, ulong offset);
    [DllImport(Lib)] public static extern int vkMapMemory(nint device, nint memory, ulong offset, ulong size, uint flags, void** data);
    [DllImport(Lib)] public static extern int vkCreateCommandPool(nint device, VkCommandPoolCreateInfo* info, nint alloc, nint* pool);
    [DllImport(Lib)] public static extern void vkDestroyCommandPool(nint device, nint pool, nint alloc);
    [DllImport(Lib)] public static extern int vkAllocateCommandBuffers(nint device, VkCommandBufferAllocateInfo* info, nint* buffers);
    [DllImport(Lib)] public static extern int vkBeginCommandBuffer(nint cb, VkCommandBufferBeginInfo* info);
    [DllImport(Lib)] public static extern int vkEndCommandBuffer(nint cb);
    [DllImport(Lib)] public static extern int vkResetCommandBuffer(nint cb, uint flags);
    [DllImport(Lib)] public static extern void vkCmdPipelineBarrier(nint cb, uint srcStage, uint dstStage, uint dependencyFlags,
        uint memoryBarrierCount, nint memoryBarriers, uint bufferBarrierCount, nint bufferBarriers,
        uint imageBarrierCount, VkImageMemoryBarrier* imageBarriers);
    [DllImport(Lib)] public static extern void vkCmdCopyBufferToImage(nint cb, nint buffer, nint image, uint layout, uint regionCount, VkBufferImageCopy* regions);

    public static void Check(int result, string call)
    {
        if (result < 0)
            throw new InvalidOperationException($"Vulkan call {call} failed with VkResult {result}.");
    }
}
