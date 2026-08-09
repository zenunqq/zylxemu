// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Threading;

namespace ZylxEmu.Libs.Kernel;

/// <summary>
/// POSIX pthread barrier implementation for PS4/PS5 guests.
///
/// A barrier blocks all <c>count</c> threads that call <c>pthread_barrier_wait</c>
/// until the last one arrives, then releases them all simultaneously.  The last
/// arrival receives <c>PTHREAD_BARRIER_SERIAL_THREAD</c> (−1 as int); all others
/// receive 0.
///
/// Games that use TBB, Bullet, or bespoke thread pools often synchronize worker
/// packs with barriers.  Without this, the game hangs at its first
/// <c>pthread_barrier_wait</c> call because the workers never rendezvous.
/// </summary>
public static class KernelPthreadBarrierCompatExports
{
    // POSIX defines PTHREAD_BARRIER_SERIAL_THREAD as a non-zero value returned
    // to exactly one thread per rendezvous — conventionally -1.
    private const int PthreadBarrierSerialThread = -1;

    private const ulong SyntheticBarrierHandleBase = 0x00006006_0000_0000UL;
    private static long _nextSyntheticBarrierHandleId = 1;

    private static readonly object _stateGate = new();
    private static readonly Dictionary<ulong, BarrierState> _barrierStates = new();

    private static readonly bool _traceBarriers =
        string.Equals(Environment.GetEnvironmentVariable("ZYLXEMU_LOG_PTHREADS"), "1", StringComparison.Ordinal) ||
        string.Equals(Environment.GetEnvironmentVariable("ZYLXEMU_LOG_PTHREAD_BARRIERS"), "1", StringComparison.Ordinal);

    private sealed class BarrierState
    {
        private int _arrivedCount;
        private ulong _generation;

        public int Count { get; }
        public object SyncRoot { get; } = new();

        public BarrierState(int count) => Count = count;

        /// <summary>
        /// Arrive at the barrier. Returns <c>true</c> if this thread is the
        /// serial thread (last to arrive), <c>false</c> otherwise. Blocks
        /// until all <see cref="Count"/> threads have arrived.
        /// </summary>
        public bool ArriveAndWait(CpuContext ctx, string wakeKey)
        {
            lock (SyncRoot)
            {
                _arrivedCount++;
                if (_arrivedCount < Count)
                {
                    // Not the last arrival: park until the last thread wakes us.
                    var capturedGeneration = _generation;

                    if (GuestThreadExecution.IsGuestThread &&
                        GuestThreadExecution.TryGetCurrentImportCallFrame(out _) &&
                        GuestThreadExecution.RequestCurrentThreadBlock(
                            ctx,
                            "pthread_barrier_wait",
                            wakeKey,
                            resumeHandler: () => (int)OrbisGen2Result.ORBIS_GEN2_OK,
                            wakeHandler: () =>
                            {
                                lock (SyncRoot) { return _generation != capturedGeneration; }
                            },
                            deadline: null))
                    {
                        // Will be woken by the serial thread.
                        return false;
                    }

                    while (_generation == capturedGeneration)
                    {
                        Monitor.Wait(SyncRoot);
                    }

                    return false;
                }

                // Last arrival — reset for reuse and release all waiters.
                _arrivedCount = 0;
                _generation++;
                Monitor.PulseAll(SyncRoot);
                return true;
            }
        }
    }

    // ─── Barrier init / destroy ──────────────────────────────────────────────

    [SysAbiExport(
        Nid = "0akem2J2ags",
        ExportName = "scePthreadBarrierInit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadBarrierInit(CpuContext ctx)
    {
        var barrierAddress = ctx[CpuRegister.Rdi];
        // attr (rsi) is deliberately ignored — PS5 barriers do not expose
        // any attributes that affect behaviour in our host implementation.
        var count = unchecked((int)ctx[CpuRegister.Rdx]);

        if (barrierAddress == 0 || count <= 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        var syntheticHandle = AllocateSyntheticHandle();
        var state = new BarrierState(count);

        lock (_stateGate)
        {
            _barrierStates[barrierAddress] = state;
            _barrierStates[syntheticHandle] = state;
        }

        if (!KernelMemoryCompatExports.TryWriteUInt64Compat(ctx, barrierAddress, syntheticHandle))
        {
            lock (_stateGate)
            {
                _barrierStates.Remove(barrierAddress);
                _barrierStates.Remove(syntheticHandle);
            }
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        if (_traceBarriers)
        {
            Console.Error.WriteLine(
                $"[LOADER][TRACE] pthread_barrier_init: addr=0x{barrierAddress:X16} handle=0x{syntheticHandle:X16} count={count}");
        }

        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "WoH7vl3POBY",
        ExportName = "pthread_barrier_init",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadBarrierInit(CpuContext ctx) => PthreadBarrierInit(ctx);

    [SysAbiExport(
        Nid = "c0Zf5TB-yEw",
        ExportName = "scePthreadBarrierDestroy",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadBarrierDestroy(CpuContext ctx)
    {
        var barrierAddress = ctx[CpuRegister.Rdi];
        if (barrierAddress == 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        var resolvedAddress = ResolveBarrierHandle(ctx, barrierAddress);

        BarrierState? state;
        lock (_stateGate)
        {
            _barrierStates.TryGetValue(resolvedAddress, out state);
        }

        if (state is null)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        }

        lock (state.SyncRoot)
        {
            // POSIX: destroying a barrier with waiters is undefined behaviour.
            // Log a warning but proceed to avoid hard-crash on cleanup paths.
        }

        lock (_stateGate)
        {
            _barrierStates.Remove(barrierAddress);
            if (resolvedAddress != barrierAddress)
            {
                _barrierStates.Remove(resolvedAddress);
            }
        }

        _ = KernelMemoryCompatExports.TryWriteUInt64Compat(ctx, barrierAddress, 0);

        if (_traceBarriers)
        {
            Console.Error.WriteLine(
                $"[LOADER][TRACE] pthread_barrier_destroy: addr=0x{barrierAddress:X16}");
        }

        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "hKmHQgIVH8U",
        ExportName = "pthread_barrier_destroy",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadBarrierDestroy(CpuContext ctx) => PthreadBarrierDestroy(ctx);

    // ─── Barrier wait ────────────────────────────────────────────────────────

    [SysAbiExport(
        Nid = "yFApe4OYMFE",
        ExportName = "scePthreadBarrierWait",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadBarrierWait(CpuContext ctx)
    {
        var barrierAddress = ctx[CpuRegister.Rdi];
        if (barrierAddress == 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        if (!TryResolveBarrierState(ctx, barrierAddress, out var resolvedAddress, out var state))
        {
            // Auto-initialize a zero barrier as a degenerate 1-thread barrier
            // (the game leaked a static-initializer barrier; this avoids a crash
            // while still logging so the caller can fix their init).
            Console.Error.WriteLine(
                $"[LOADER][WARN] pthread_barrier_wait on uninitialised barrier 0x{barrierAddress:X16}; auto-init count=1");
            ctx[CpuRegister.Rax] = unchecked((ulong)PthreadBarrierSerialThread);
            return (int)OrbisGen2Result.ORBIS_GEN2_OK;
        }

        var wakeKey = $"pthread_barrier:0x{resolvedAddress:X16}";
        var isSerial = state.ArriveAndWait(ctx, wakeKey);

        if (isSerial)
        {
            // Wake all cooperative waiters parked on this barrier.
            _ = GuestThreadExecution.Scheduler?.WakeBlockedThreads(wakeKey);
        }

        if (_traceBarriers)
        {
            Console.Error.WriteLine(
                $"[LOADER][TRACE] pthread_barrier_wait: addr=0x{barrierAddress:X16} serial={isSerial}");
        }

        ctx[CpuRegister.Rax] = isSerial
            ? unchecked((ulong)PthreadBarrierSerialThread)
            : 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "OgCUxBtPT18",
        ExportName = "pthread_barrier_wait",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadBarrierWait(CpuContext ctx) => PthreadBarrierWait(ctx);

    // ─── Barrier attr (stubs — PS5 barriers have no meaningful attributes) ───

    [SysAbiExport(
        Nid = "Np5oel5bQEg",
        ExportName = "scePthreadBarrierattrInit",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadBarrierattrInit(CpuContext ctx)
    {
        var attrAddress = ctx[CpuRegister.Rdi];
        if (attrAddress == 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        // Write a non-zero sentinel so destroy can validate it was initialised.
        Span<byte> sentinel = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(sentinel, 1);
        _ = ctx.Memory.TryWrite(attrAddress, sentinel);
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "e-N+wYKtCEk",
        ExportName = "pthread_barrierattr_init",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadBarrierattrInit(CpuContext ctx) => PthreadBarrierattrInit(ctx);

    [SysAbiExport(
        Nid = "BI6GHan6Xb8",
        ExportName = "scePthreadBarrierattrDestroy",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PthreadBarrierattrDestroy(CpuContext ctx)
    {
        var attrAddress = ctx[CpuRegister.Rdi];
        if (attrAddress == 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> zero = stackalloc byte[sizeof(uint)];
        _ = ctx.Memory.TryWrite(attrAddress, zero);
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "3o9nKAXmaA8",
        ExportName = "pthread_barrierattr_destroy",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int PosixPthreadBarrierattrDestroy(CpuContext ctx) => PthreadBarrierattrDestroy(ctx);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static ulong AllocateSyntheticHandle()
    {
        var id = unchecked((ulong)Interlocked.Increment(ref _nextSyntheticBarrierHandleId));
        return SyntheticBarrierHandleBase + (id << 4);
    }

    private static ulong ResolveBarrierHandle(CpuContext ctx, ulong barrierAddress)
    {
        lock (_stateGate)
        {
            if (_barrierStates.ContainsKey(barrierAddress))
            {
                return barrierAddress;
            }
        }

        if (KernelMemoryCompatExports.TryReadUInt64Compat(ctx, barrierAddress, out var pointedHandle) &&
            pointedHandle != 0)
        {
            lock (_stateGate)
            {
                if (_barrierStates.ContainsKey(pointedHandle))
                {
                    return pointedHandle;
                }
            }
        }

        return barrierAddress;
    }

    private static bool TryResolveBarrierState(
        CpuContext ctx,
        ulong barrierAddress,
        out ulong resolvedAddress,
        out BarrierState? state)
    {
        resolvedAddress = barrierAddress;
        state = null;

        lock (_stateGate)
        {
            if (_barrierStates.TryGetValue(barrierAddress, out state))
            {
                return true;
            }
        }

        if (!KernelMemoryCompatExports.TryReadUInt64Compat(ctx, barrierAddress, out var pointedHandle) ||
            pointedHandle == 0)
        {
            return false;
        }

        lock (_stateGate)
        {
            if (_barrierStates.TryGetValue(pointedHandle, out state))
            {
                _barrierStates[barrierAddress] = state;
                resolvedAddress = pointedHandle;
                return true;
            }
        }

        return false;
    }

    private static int SetReturn(CpuContext ctx, OrbisGen2Result result)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)(int)result);
        return (int)result;
    }
}
