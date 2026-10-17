using System.Runtime.InteropServices;

namespace NovaUI.Desktop.Platforms.Linux;

/// <summary>Vulkan instance plus the logical device shared by all windows.</summary>
internal sealed unsafe class VulkanDevice : IDisposable
{
    public nint Instance { get; private set; }
    public nint PhysicalDevice { get; private set; }
    public nint Device { get; private set; }
    public nint Queue { get; private set; }
    public uint QueueFamily { get; private set; }
    public bool IsInitialized => Device != 0;

    public VulkanDevice()
    {
        using var extensions = new Utf8Array("VK_KHR_surface", "VK_KHR_xlib_surface");
        var info = new VkInstanceCreateInfo
        {
            sType = 1,
            enabledExtensionCount = extensions.Count,
            ppEnabledExtensionNames = extensions.Pointer,
        };
        nint instance;
        Vk.Check(Vk.vkCreateInstance(&info, 0, &instance), nameof(Vk.vkCreateInstance));
        Instance = instance;
    }

    public nint CreateSurface(nint display, nint window)
    {
        var info = new VkXlibSurfaceCreateInfo { sType = 1000004000, dpy = display, window = window };
        nint surface;
        Vk.Check(Vk.vkCreateXlibSurfaceKHR(Instance, &info, 0, &surface), nameof(Vk.vkCreateXlibSurfaceKHR));
        return surface;
    }

    public void DestroySurface(nint surface) => Vk.vkDestroySurfaceKHR(Instance, surface, 0);

    /// <summary>Picks a GPU/queue that can present to <paramref name="surface"/> and creates the logical device.</summary>
    public void Initialize(nint surface)
    {
        if (IsInitialized)
            return;

        uint deviceCount = 0;
        Vk.Check(Vk.vkEnumeratePhysicalDevices(Instance, &deviceCount, null), nameof(Vk.vkEnumeratePhysicalDevices));
        if (deviceCount == 0)
            throw new InvalidOperationException("No Vulkan capable GPU found.");

        var devices = new nint[deviceCount];
        fixed (nint* pDevices = devices)
            Vk.Check(Vk.vkEnumeratePhysicalDevices(Instance, &deviceCount, pDevices), nameof(Vk.vkEnumeratePhysicalDevices));

        foreach (var device in devices)
        {
            uint familyCount = 0;
            Vk.vkGetPhysicalDeviceQueueFamilyProperties(device, &familyCount, null);
            var families = new VkQueueFamilyProperties[familyCount];
            fixed (VkQueueFamilyProperties* pFamilies = families)
                Vk.vkGetPhysicalDeviceQueueFamilyProperties(device, &familyCount, pFamilies);

            for (uint i = 0; i < familyCount; i++)
            {
                uint supported = 0;
                Vk.vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, &supported);
                if ((families[i].queueFlags & 1) == 0 || supported == 0)
                    continue;

                PhysicalDevice = device;
                QueueFamily = i;
                CreateLogicalDevice();
                return;
            }
        }

        throw new InvalidOperationException("No Vulkan queue can present to the window surface.");
    }

    private void CreateLogicalDevice()
    {
        float priority = 1f;
        var queueInfo = new VkDeviceQueueCreateInfo { sType = 2, queueFamilyIndex = QueueFamily, queueCount = 1, pQueuePriorities = &priority };
        using var extensions = new Utf8Array("VK_KHR_swapchain");
        var info = new VkDeviceCreateInfo
        {
            sType = 3,
            queueCreateInfoCount = 1,
            pQueueCreateInfos = &queueInfo,
            enabledExtensionCount = extensions.Count,
            ppEnabledExtensionNames = extensions.Pointer,
        };
        nint device, queue;
        Vk.Check(Vk.vkCreateDevice(PhysicalDevice, &info, 0, &device), nameof(Vk.vkCreateDevice));
        Vk.vkGetDeviceQueue(device, QueueFamily, 0, &queue);
        Device = device;
        Queue = queue;
    }

    public uint FindHostVisibleMemoryType(uint typeBits)
    {
        const uint wanted = 2 | 4; // HOST_VISIBLE | HOST_COHERENT
        byte* props = stackalloc byte[520];
        Vk.vkGetPhysicalDeviceMemoryProperties(PhysicalDevice, props);
        var count = *(uint*)props;
        for (uint i = 0; i < count; i++)
        {
            var flags = *(uint*)(props + 4 + i * 8);
            if ((typeBits & (1u << (int)i)) != 0 && (flags & wanted) == wanted)
                return i;
        }

        throw new InvalidOperationException("No host-visible Vulkan memory type available.");
    }

    public void Dispose()
    {
        if (Device != 0)
        {
            Vk.vkDeviceWaitIdle(Device);
            Vk.vkDestroyDevice(Device, 0);
            Device = 0;
        }

        if (Instance != 0)
        {
            Vk.vkDestroyInstance(Instance, 0);
            Instance = 0;
        }
    }

    private sealed class Utf8Array : IDisposable
    {
        private readonly byte** _items;

        public Utf8Array(params string[] values)
        {
            Count = (uint)values.Length;
            _items = (byte**)NativeMemory.Alloc((nuint)values.Length, (nuint)sizeof(nint));
            for (var i = 0; i < values.Length; i++)
                _items[i] = (byte*)Marshal.StringToCoTaskMemUTF8(values[i]);
        }

        public uint Count { get; }
        public byte** Pointer => _items;

        public void Dispose()
        {
            for (var i = 0; i < Count; i++)
                Marshal.FreeCoTaskMem((nint)_items[i]);
            NativeMemory.Free(_items);
        }
    }
}
