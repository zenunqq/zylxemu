// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE;
using System.Buffers.Binary;

namespace ZylxEmu.Libs.Np;

public static class NpTrophy2Exports
{
    private static int _nextContext = 1;
    private static int _nextHandle = 1;

    // ─── Library init / term ─────────────────────────────────────────────────
    // sceNpTrophy2Init and sceNpTrophy2Term are required by many titles before
    // any context or handle operations are possible. Without them, the game
    // calls sceNpTrophy2CreateContext, receives a NOT_INITIALIZED error, and
    // hard-crashes or loops forever waiting on a trophy event.

    [SysAbiExport(
        Nid = "SXEpPKbJmkc",
        ExportName = "sceNpTrophy2Init",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2Init(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "qNkPu2HVSJI",
        ExportName = "sceNpTrophy2Term",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2Term(CpuContext ctx) => ReturnOk(ctx);

    // ─── Trophy unlock ───────────────────────────────────────────────────────
    // sceNpTrophy2UnlockTrophy is called on trophy-earning events. Returning
    // NOT_FOUND here (as GetTrophyInfo does) causes some titles to abort; a
    // graceful ALREADY_UNLOCKED response is safer and is a documented outcome.

    private const int OrbisNpTrophy2ErrorAlreadyUnlocked = unchecked((int)0x80551604);

    [SysAbiExport(
        Nid = "pDCN0STVMFI",
        ExportName = "sceNpTrophy2UnlockTrophy",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2UnlockTrophy(CpuContext ctx)
    {
        // context, handle, trophyId, outDetails*, outData* — all ignored.
        // Report ALREADY_UNLOCKED: callers treat this as benign and continue.
        return SetReturn(ctx, (OrbisGen2Result)OrbisNpTrophy2ErrorAlreadyUnlocked);
    }

    // ─── Trophy unlock state ─────────────────────────────────────────────────
    // sceNpTrophy2GetTrophyUnlockState fills a bitfield array indicating which
    // trophies have been unlocked. Return an all-zero (none unlocked) set so
    // callers do not see garbage and attempt to double-unlock.

    [SysAbiExport(
        Nid = "1YFTL-A4Bw8",
        ExportName = "sceNpTrophy2GetTrophyUnlockState",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2GetTrophyUnlockState(CpuContext ctx)
    {
        // context, handle, flagsArray*, numFlags* — zero the output.
        var flagsArrayAddress = ctx[CpuRegister.Rdx];
        var numFlagsAddress   = ctx[CpuRegister.Rcx];

        if (flagsArrayAddress != 0)
        {
            // The flags array is at most 128 trophies / 32 bits = 16 uint32s = 64 bytes.
            Span<byte> zero = stackalloc byte[64];
            zero.Clear();
            _ = ctx.Memory.TryWrite(flagsArrayAddress, zero);
        }

        if (numFlagsAddress != 0)
        {
            Span<byte> zero = stackalloc byte[sizeof(uint)];
            zero.Clear();
            _ = ctx.Memory.TryWrite(numFlagsAddress, zero);
        }

        return ReturnOk(ctx);
    }

    [SysAbiExport(
        Nid = "Bagshr7OQ6Q",
        ExportName = "sceNpTrophy2CreateContext",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2CreateContext(CpuContext ctx)
    {
        return WriteIdAndReturn(ctx, ctx[CpuRegister.Rdi], ref _nextContext);
    }

    [SysAbiExport(
        Nid = "Gz1rmUZpROM",
        ExportName = "sceNpTrophy2CreateHandle",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2CreateHandle(CpuContext ctx)
    {
        return WriteIdAndReturn(ctx, ctx[CpuRegister.Rdi], ref _nextHandle);
    }

    [SysAbiExport(
        Nid = "sysY2FHYff4",
        ExportName = "sceNpTrophy2DestroyContext",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2DestroyContext(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "d8P11CI40KE",
        ExportName = "sceNpTrophy2DestroyHandle",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2DestroyHandle(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "fYapWA9xVmA",
        ExportName = "sceNpTrophy2AbortHandle",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2AbortHandle(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "bIDov3wBu5Q",
        ExportName = "sceNpTrophy2RegisterContext",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2RegisterContext(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "sUXGfNMalIo",
        ExportName = "sceNpTrophy2RegisterUnlockCallback",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2RegisterUnlockCallback(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "wVqxM58sIKs",
        ExportName = "sceNpTrophy2UnregisterUnlockCallback",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2UnregisterUnlockCallback(CpuContext ctx) => ReturnOk(ctx);

    [SysAbiExport(
        Nid = "EHQEDVXZ0TI",
        ExportName = "sceNpTrophy2ShowTrophyList",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2ShowTrophyList(CpuContext ctx) => ReturnOk(ctx);

    /// <summary>
    /// Gen5 ABI: context, handle, trophy id, then SceNpTrophy2Details and
    /// SceNpTrophy2Data output pointers.
    /// </summary>
    /// <remarks>
    /// Reports "no such trophy" rather than succeeding. Succeeding would require
    /// filling both output structures, and their exact layouts are not confirmed
    /// here — a title that trusted zeroed details would read an empty name and a
    /// grade of zero as real data. NOT_FOUND is a documented outcome that callers
    /// must already handle, so it degrades along a path the game tests.
    /// </remarks>
    [SysAbiExport(
        Nid = "EwNylPdWUTM",
        ExportName = "sceNpTrophy2GetTrophyInfo",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2GetTrophyInfo(CpuContext ctx) =>
        SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);

    [SysAbiExport(
        Nid = "y3zHpdZO6ME",
        ExportName = "sceNpTrophy2GetTrophyInfoArray",
        Target = Generation.Gen5,
        LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2GetTrophyInfoArray(CpuContext ctx) =>
        SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_NOT_FOUND);


    private static int WriteIdAndReturn(CpuContext ctx, ulong outAddress, ref int nextId)
    {
        if (outAddress == 0)
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> idBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(idBytes, nextId);
        if (!ctx.Memory.TryWrite(outAddress, idBytes))
        {
            return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        nextId++;
        return SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);
    }

    private static int ReturnOk(CpuContext ctx) => SetReturn(ctx, OrbisGen2Result.ORBIS_GEN2_OK);

    private static int SetReturn(CpuContext ctx, OrbisGen2Result result)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)(int)result);
        return (int)result;
    }
}
