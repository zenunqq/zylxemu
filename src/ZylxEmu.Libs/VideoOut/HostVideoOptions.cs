// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.Libs.VideoOut;

using ZylxEmu.Libs.Gpu.Metal;

public enum HostWindowMode
{
    Windowed,
    Borderless,
    ExclusiveFullscreen,
}

public enum HostScalingMode
{
    Fit,
    Cover,
    Stretch,
    Integer,
}

public enum HostHdrMode
{
    Auto,
    On,
    Off,
}

public sealed record HostVideoOptions
{
    public static HostVideoOptions Default { get; } = new();

    public HostWindowMode WindowMode { get; init; } = HostWindowMode.Windowed;

    public HostScalingMode ScalingMode { get; init; } = HostScalingMode.Fit;

    public int Width { get; init; } = 1920;

    public int Height { get; init; } = 1080;

    public int DisplayIndex { get; init; }

    public int RefreshRate { get; init; }

    public bool VSync { get; init; } = true;

    public HostHdrMode HdrMode { get; init; } = HostHdrMode.Auto;

    /// <summary>
    /// Maximum frames per second the presenter will submit. 0 = unlimited.
    /// Override with ZYLXEMU_MAX_FPS environment variable.
    /// </summary>
    public int MaxFps { get; init; } = ResolveMaxFps();

    /// <summary>
    /// When true the presenter uses a busy-wait after each frame to hit the
    /// MaxFps target precisely rather than relying on VSync alone.
    /// Reduces jitter at the cost of a CPU core. Default: false.
    /// </summary>
    public bool PreciseFramePacing { get; init; } = false;

    private static int ResolveMaxFps()
    {
        var raw = Environment.GetEnvironmentVariable("ZYLXEMU_MAX_FPS");
        return int.TryParse(raw, out var fps) && fps > 0 ? fps : 0;
    }

    public HostVideoOptions Normalize() => this with
    {
        Width = Math.Clamp(Width, 640, 16384),
        Height = Math.Clamp(Height, 360, 16384),
        DisplayIndex = Math.Max(0, DisplayIndex),
        RefreshRate = Math.Clamp(RefreshRate, 0, 1000),
        HdrMode = Enum.IsDefined(HdrMode) ? HdrMode : HostHdrMode.Auto,
        MaxFps = Math.Max(0, MaxFps),
    };
}

public static class HostVideoHost
{
    public static bool TryConfigureVideo(HostVideoOptions options)
    {
        var normalized = options.Normalize();
        return VulkanVideoPresenter.TryConfigureVideo(normalized) &
               MetalVideoPresenter.TryConfigureVideo(normalized);
    }
}
