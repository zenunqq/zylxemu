// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Diagnostics;

namespace ZylxEmu.Libs;

/// <summary>
/// High-resolution host sleeps for guest pacing.
///
/// Enhancement: at startup the module measures the scheduler's actual wakeup
/// jitter (average overshoot of Thread.Sleep(1)) and uses that calibrated
/// value as the coarse/fine crossover threshold. This means the emulator
/// automatically uses the right margin on hosts with both tight (Windows MMCSS,
/// Linux with RT kernel) and loose (default Windows, macOS) schedulers instead
/// of hard-coding 1.2 ms.
///
/// The calibration costs ~50 ms on the first call and is cached for the
/// lifetime of the process.
/// </summary>
internal static class HostTiming
{
    // Calibrated scheduler overshoot in microseconds (Thread.Sleep(1) actual).
    private static readonly long _schedulerOvershootUs = CalibrateScheduler();

    private static long CalibrateScheduler()
    {
        // Disable calibration when ZYLXEMU_SKIP_TIMING_CALIBRATION=1.
        if (string.Equals(
                Environment.GetEnvironmentVariable("ZYLXEMU_SKIP_TIMING_CALIBRATION"),
                "1", StringComparison.Ordinal))
        {
            return 1200; // conservative fallback matching original value
        }

        const int Samples = 10;
        var total = 0L;
        for (var i = 0; i < Samples; i++)
        {
            var before = Stopwatch.GetTimestamp();
            Thread.Sleep(1);
            var elapsed = (Stopwatch.GetTimestamp() - before) * 1_000_000 / Stopwatch.Frequency;
            total += Math.Max(elapsed - 1000, 0); // overshoot relative to 1 ms target
        }

        var avg = total / Samples;
        // Clamp: at least 200 µs (very fast scheduler) and at most 4000 µs (very slow).
        var clamped = Math.Clamp(avg + 200, 200, 4000);
        Console.Error.WriteLine(
            $"[TIMING] Scheduler calibration: avg overshoot {avg} µs → threshold {clamped} µs");
        return clamped;
    }

    /// <summary>
    /// Blocks until <see cref="Stopwatch.GetTimestamp"/> reaches
    /// <paramref name="targetTimestamp"/>.
    /// </summary>
    public static void SleepUntil(long targetTimestamp)
    {
        while (true)
        {
            var remainingTicks = targetTimestamp - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            if (remainingTicks > Stopwatch.Frequency * 60)
            {
                Thread.Sleep(30_000);
                continue;
            }

            var remainingMicroseconds = remainingTicks * 1_000_000 / Stopwatch.Frequency;

            // Use calibrated overshoot: sleep coarsely only when we have
            // enough headroom that the overshoot won't cause us to sleep past
            // the target.
            if (remainingMicroseconds > _schedulerOvershootUs * 2)
            {
                var sleepMs = (int)((remainingMicroseconds - _schedulerOvershootUs) / 1000);
                if (sleepMs > 0)
                {
                    Thread.Sleep(sleepMs);
                }
            }
            else if (remainingMicroseconds > _schedulerOvershootUs)
            {
                Thread.Sleep(1);
            }
            else if (remainingMicroseconds > 100)
            {
                Thread.Sleep(0);
            }
            else
            {
                Thread.SpinWait(64);
            }
        }
    }

    /// <summary>Blocks for the given number of microseconds.</summary>
    public static void SleepMicroseconds(long microseconds)
    {
        if (microseconds <= 0)
        {
            return;
        }

        if (microseconds >= 10_000_000)
        {
            Thread.Sleep((int)Math.Min(microseconds / 1000, int.MaxValue));
            return;
        }

        var ticks = microseconds * Stopwatch.Frequency / 1_000_000;
        SleepUntil(Stopwatch.GetTimestamp() + ticks);
    }

    /// <summary>Calibrated scheduler overshoot in microseconds (diagnostic).</summary>
    public static long SchedulerOvershootMicroseconds => _schedulerOvershootUs;
}
