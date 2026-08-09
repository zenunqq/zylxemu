// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE;
using System.Buffers.Binary;
using System.Threading;

namespace ZylxEmu.Libs.Kernel;

/// <summary>
/// Success stubs for kernel syscalls that are commonly unimplemented and cause
/// games to crash or hang before reaching first-frame.
///
/// Grouped by subsystem:
///  • Memory locking (mlock / munlock / mlockall / munlockall)
///  • Resource usage (getrusage)
///  • Process identity (sceKernelSetProcessName / sceKernelGetCpumode)
///  • Miscellaneous libc/kernel gaps
///
/// All stubs either return 0 (success) or write a safe default value and return
/// 0. None of them gate gameplay-critical behaviour on a real return value.
/// </summary>
public static class KernelMissingSyscallsExports
{
    // ─── Memory locking ──────────────────────────────────────────────────────
    // The PS5 kernel supports mlock so games can pin GPU-visible buffers.
    // On the host OS the allocations are already wired, so these are no-ops.

    [SysAbiExport(
        Nid = "mS-Tdm8bBto",
        ExportName = "mlock",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Mlock(CpuContext ctx)
    {
        // addr=rdi len=rsi — both ignored; host memory is always wired.
        return Ok(ctx);
    }

    [SysAbiExport(
        Nid = "9kDHcJBQQNg",
        ExportName = "munlock",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Munlock(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(
        Nid = "n15PN2HFVJY",
        ExportName = "mlockall",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Mlockall(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(
        Nid = "f1BuBnHMPMs",
        ExportName = "munlockall",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Munlockall(CpuContext ctx) => Ok(ctx);

    // ─── Resource usage ──────────────────────────────────────────────────────
    // getrusage is called by profiling shims and some middleware init paths.
    // Return a zeroed struct so callers get clean data rather than garbage.

    // struct rusage is 144 bytes on PS5 (mirrors the BSD layout).
    private const int RusageStructSize = 144;

    [SysAbiExport(
        Nid = "grD2PaBHvGQ",
        ExportName = "getrusage",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Getrusage(CpuContext ctx)
    {
        // who=rdi usage=rsi
        var usageAddress = ctx[CpuRegister.Rsi];
        if (usageAddress == 0)
        {
            return SetErrno(ctx, 14 /* EFAULT */);
        }

        Span<byte> zero = stackalloc byte[RusageStructSize];
        zero.Clear();
        if (!ctx.Memory.TryWrite(usageAddress, zero))
        {
            return SetErrno(ctx, 14 /* EFAULT */);
        }

        return Ok(ctx);
    }

    // ─── Process identity ────────────────────────────────────────────────────

    [SysAbiExport(
        Nid = "rZSMxNcRqLE",
        ExportName = "sceKernelSetProcessName",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelSetProcessName(CpuContext ctx)
    {
        // name=rdi — ignored; no host process rename needed.
        return Ok(ctx);
    }

    [SysAbiExport(
        Nid = "l8bNSTsTsIk",
        ExportName = "sceKernelGetCpumode",
        Target = Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelGetCpumode(CpuContext ctx)
    {
        // Returns 0 = normal CPU mode (not low-power). The PS5 SDK only uses
        // the non-zero modes for specific devkit configurations.
        return Ok(ctx);
    }

    // ─── Thread / scheduling helpers ─────────────────────────────────────────

    [SysAbiExport(
        Nid = "zNgOsPNn9NQ",
        ExportName = "sceKernelSetThreadDtors",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelSetThreadDtors(CpuContext ctx)
    {
        // Called by some runtime initializers to register C++ per-thread
        // destructors. We run __cxa_atexit destructors at exit; per-thread ones
        // are a no-op since the guest thread exits cleanly through KernelExports.
        return Ok(ctx);
    }

    [SysAbiExport(
        Nid = "A+uDpsVzteM",
        ExportName = "sceKernelGetCurrentCpu",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelGetCurrentCpu(CpuContext ctx)
    {
        // Returns a logical CPU index [0, 7]. Games use it for diagnostic
        // logging; returning 0 is always safe.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "q3brjBdVAss",
        ExportName = "sceKernelSetThreadName",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelSetThreadName(CpuContext ctx)
    {
        // tid=rdi name=rsi — not critical; silently accept.
        return Ok(ctx);
    }

    // ─── sysctl family ───────────────────────────────────────────────────────
    // Games query sysctl for hardware info (CPU count, memory size). Return
    // safe defaults that let the game continue without trusting real values.

    [SysAbiExport(
        Nid = "pBFGCl-QbEA",
        ExportName = "sysctl",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Sysctl(CpuContext ctx)
    {
        // name=rdi namelen=rsi oldp=rdx oldlenp=rcx newp=r8 newlen=r9
        // If the caller supplied an output buffer, zero it so they don't see
        // garbage; then return ENOENT so the caller falls back gracefully.
        var oldp = ctx[CpuRegister.Rdx];
        var oldlenp = ctx[CpuRegister.Rcx];
        if (oldlenp != 0 && ctx.TryReadUInt64(oldlenp, out var oldLen) && oldLen > 0 && oldp != 0)
        {
            var cap = (int)Math.Min(oldLen, 4096);
            Span<byte> zero = stackalloc byte[Math.Min(cap, 256)];
            zero.Clear();
            _ = ctx.Memory.TryWrite(oldp, zero);
        }

        return SetErrno(ctx, 2 /* ENOENT */);
    }

    [SysAbiExport(
        Nid = "4KWpZBdQVqE",
        ExportName = "sysctlbyname",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int Sysctlbyname(CpuContext ctx)
    {
        // name=rdi namelen=rsi oldp=rdx oldlenp=rcx newp=r8 newlen=r9
        // Same strategy: zero output and return ENOENT.
        var oldp = ctx[CpuRegister.Rdx];
        var oldlenp = ctx[CpuRegister.Rcx];
        if (oldlenp != 0 && ctx.TryReadUInt64(oldlenp, out var oldLen) && oldLen > 0 && oldp != 0)
        {
            var cap = (int)Math.Min(oldLen, 4096);
            Span<byte> zero = stackalloc byte[Math.Min(cap, 256)];
            zero.Clear();
            _ = ctx.Memory.TryWrite(oldp, zero);
        }

        return SetErrno(ctx, 2 /* ENOENT */);
    }

    // ─── Miscellaneous libc gaps ──────────────────────────────────────────────

    [SysAbiExport(
        Nid = "9-H+J6TDkBE",
        ExportName = "sceKernelGetCompiledSdkVersionByAddr",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelGetCompiledSdkVersionByAddr(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "OMDRKKAZ8I8",
        ExportName = "sceKernelSetThreadVfpException",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelSetThreadVfpException(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(
        Nid = "7H0BQFSC-j4",
        ExportName = "sceKernelGetFsSandboxRandomWord",
        Target = Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelGetFsSandboxRandomWord(CpuContext ctx)
    {
        // Returns a pseudo-random 64-bit word used as a filesystem sandbox salt.
        // Games that fail this call abort() because they cannot mount their data
        // partition. Return a stable non-zero value so the path resolves.
        ctx[CpuRegister.Rax] = 0xDEADBEEFCAFEBABEUL;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "MpMDMQB02lQ",
        ExportName = "sceKernelSetFsSandboxRandomWord",
        Target = Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelSetFsSandboxRandomWord(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(
        Nid = "9lFYnCFP+k4",
        ExportName = "sceKernelGetPageTableStats",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelGetPageTableStats(CpuContext ctx)
    {
        var outAddress = ctx[CpuRegister.Rdi];
        if (outAddress != 0)
        {
            // Zero the output struct; size is 0x18 (3 × u64).
            Span<byte> zero = stackalloc byte[0x18];
            zero.Clear();
            _ = ctx.Memory.TryWrite(outAddress, zero);
        }

        return Ok(ctx);
    }

    [SysAbiExport(
        Nid = "cGKbgxAHHmM",
        ExportName = "sceKernelDlsym",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libKernel")]
    public static int KernelDlsym(CpuContext ctx)
    {
        // handle=rdi name=rsi out=rdx
        // We don't support dynamic symbol lookup outside the HLE layer;
        // return NOT_FOUND so the caller can degrade gracefully.
        var outAddress = ctx[CpuRegister.Rdx];
        if (outAddress != 0)
        {
            Span<byte> zero = stackalloc byte[sizeof(ulong)];
            zero.Clear();
            _ = ctx.Memory.TryWrite(outAddress, zero);
        }

        ctx[CpuRegister.Rax] = unchecked((ulong)(int)OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);
        return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static int Ok(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    /// <summary>
    /// Sets RAX to the negated errno value and returns that value, matching
    /// the PS5 libc errno-in-RAX calling convention for BSD-heritage syscalls.
    /// </summary>
    private static int SetErrno(CpuContext ctx, int errno)
    {
        var result = unchecked((ulong)-errno);
        ctx[CpuRegister.Rax] = result;
        return unchecked((int)result);
    }
}
