// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZylxEmu.Libs;

/// <summary>
/// Pins emulator threads to specific logical CPU cores when the host OS
/// supports it, reducing cache thrashing and scheduler jitter.
///
/// The PS5's Zen 2 CPU has 8 cores; the emulator uses:
///  - Core 0: render thread (Vulkan command recording)
///  - Core 1: audio output / NGS2 mix
///  - Cores 2-5: guest CPU threads (native execution)
///  - Cores 6-7: shader compile workers + background I/O
///
/// When the host has fewer logical processors the assignment degrades
/// gracefully (no affinity set rather than crashing).
///
/// Activate with ZYLXEMU_THREAD_AFFINITY=1.
/// Disable per-role with e.g. ZYLXEMU_AFFINITY_RENDER=0.
/// </summary>
public static class ThreadAffinityManager
{
    private static readonly bool _enabled = string.Equals(
        Environment.GetEnvironmentVariable("ZYLXEMU_THREAD_AFFINITY"),
        "1", StringComparison.Ordinal);

    private static readonly int _processorCount = Environment.ProcessorCount;

    public enum Role
    {
        Render,
        Audio,
        GuestCpu,
        ShaderCompile,
        Io,
    }

    /// <summary>
    /// Applies the affinity mask for <paramref name="role"/> to the calling thread.
    /// No-op if ZYLXEMU_THREAD_AFFINITY=1 is not set.
    /// </summary>
    public static void SetCurrentThreadRole(Role role)
    {
        if (!_enabled || _processorCount < 4)
        {
            return;
        }

        var mask = ComputeMask(role);
        if (mask == 0)
        {
            return;
        }

        try
        {
            ApplyAffinityMask(mask);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[AFFINITY][WARN] Failed to set {role} affinity: {ex.Message}");
        }
    }

    /// <summary>
    /// Elevates the calling thread's priority for latency-sensitive work.
    /// </summary>
    public static void SetCurrentThreadPriority(ThreadPriority priority)
    {
        try
        {
            Thread.CurrentThread.Priority = priority;
        }
        catch
        {
            // Ignore — not fatal.
        }
    }

    private static nuint ComputeMask(Role role)
    {
        var n = _processorCount;
        return role switch
        {
            // Render thread: dedicated core 0
            Role.Render => n >= 4 ? 0b_0001UL : 0,

            // Audio: core 1
            Role.Audio => n >= 4 ? 0b_0010UL : 0,

            // Guest CPU: cores 2..min(5, n-1)
            Role.GuestCpu => BuildMask(2, Math.Min(5, n - 1)),

            // Shader compile: upper cores
            Role.ShaderCompile => BuildMask(Math.Max(6, n / 2), n - 1),

            // I/O: last core
            Role.Io => n >= 2 ? 1UL << (n - 1) : 0,

            _ => 0,
        };
    }

    private static nuint BuildMask(int first, int last)
    {
        if (first > last || first >= _processorCount)
        {
            return 0;
        }

        nuint mask = 0;
        for (var i = first; i <= Math.Min(last, _processorCount - 1); i++)
        {
            mask |= 1UL << i;
        }

        return mask;
    }

    // ── Platform-specific affinity ────────────────────────────────────────────

    private static void ApplyAffinityMask(nuint mask)
    {
        if (OperatingSystem.IsWindows())
        {
            ApplyWindowsAffinity(mask);
        }
        else if (OperatingSystem.IsLinux())
        {
            ApplyLinuxAffinity(mask);
        }
        // macOS does not expose per-thread affinity for arbitrary threads;
        // use QoS classes via pthread_set_qos_class_self_np instead.
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsAffinity(nuint mask)
    {
        var thread = GetCurrentThread();
        SetThreadAffinityMask(thread, mask);
    }

    [SupportedOSPlatform("linux")]
    private static void ApplyLinuxAffinity(nuint mask)
    {
        // cpu_set_t is 128 bytes on x86-64 Linux; we only need the first word.
        Span<ulong> cpuSet = stackalloc ulong[16];
        cpuSet[0] = (ulong)mask;
        sched_setaffinity(0, 128, ref cpuSet[0]);
    }

    [DllImport("kernel32.dll")]
    [SupportedOSPlatform("windows")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    [SupportedOSPlatform("windows")]
    private static extern nuint SetThreadAffinityMask(IntPtr hThread, nuint dwThreadAffinityMask);

    [DllImport("libc", EntryPoint = "sched_setaffinity")]
    [SupportedOSPlatform("linux")]
    private static extern int sched_setaffinity(int pid, nuint cpusetsize, ref ulong cpuset);
}
