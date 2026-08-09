// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Libs.Gpu.Metal;
using ZylxEmu.Libs.Gpu.Vulkan;

namespace ZylxEmu.Libs.Gpu;

/// <summary>
/// Process-wide access point for the guest-GPU backend, mirroring HostPlatform for the
/// host seam: static HLE export classes resolve the renderer through <see cref="Current"/>.
/// Vulkan is the default everywhere; ZYLXEMU_GPU_BACKEND=metal opts into the Metal
/// backend (macOS only) while it is being brought up. macOS flips to Metal by default
/// once the presenter reaches parity.
/// </summary>
internal static class GuestGpu
{
    private static readonly Lazy<IGuestGpuBackend> Instance = new(Create);

    public static IGuestGpuBackend Current => Instance.Value;

    private static IGuestGpuBackend Create()
    {
        var requested = Environment.GetEnvironmentVariable("ZYLXEMU_GPU_BACKEND");
        if (string.IsNullOrEmpty(requested) || requested.Equals("vulkan", StringComparison.OrdinalIgnoreCase))
        {
            return new VulkanGuestGpuBackend();
        }

        if (requested.Equals("metal", StringComparison.OrdinalIgnoreCase))
        {
            if (!OperatingSystem.IsMacOS())
            {
                Console.Error.WriteLine(
                    "[LOADER][WARN] ZYLXEMU_GPU_BACKEND=metal is only available on macOS; using Vulkan.");
                return new VulkanGuestGpuBackend();
            }

            Console.Error.WriteLine("[LOADER][INFO] GPU backend: Metal (ZYLXEMU_GPU_BACKEND).");
            return new MetalGuestGpuBackend();
        }

        Console.Error.WriteLine(
            $"[LOADER][WARN] Unknown ZYLXEMU_GPU_BACKEND value '{requested}'; using Vulkan.");
        return new VulkanGuestGpuBackend();
    }
}
