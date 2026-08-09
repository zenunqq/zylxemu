// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE;
using System.Buffers.Binary;

namespace ZylxEmu.Libs.Stubs;

/// <summary>
/// Success stubs for PS5 system calls that commonly block game boot or cause
/// black screens when they return NOT_FOUND. These were identified by tracing
/// games that stall before reaching VideoOut.
///
/// All stubs here return success (or a benign non-zero handle) so the calling
/// title can continue past its initialization phase. None of them gate
/// gameplay-critical behavior on a real return value.
/// </summary>
public static class BootCompatStubs
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static int Ok(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return 0;
    }

    private static int OkWithHandle(CpuContext ctx, CpuRegister reg)
    {
        var ptr = ctx[reg];
        if (ptr != 0)
        {
            Span<byte> handle = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(handle, 1);
            ctx.Memory.TryWrite(ptr, handle);
        }

        return Ok(ctx);
    }

    private static int OkWithSize(CpuContext ctx, CpuRegister outSizeReg, int size)
    {
        var ptr = ctx[outSizeReg];
        if (ptr != 0)
        {
            Span<byte> sizeBytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(sizeBytes, size);
            ctx.Memory.TryWrite(ptr, sizeBytes);
        }

        return Ok(ctx);
    }

    // ─── Error Dialog / IME ──────────────────────────────────────────────────

    [SysAbiExport(Nid = "gNqiZ8UUzmg", ExportName = "sceErrorDialogInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceErrorDialog")]
    public static int ErrorDialogInitialize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "CnL4Fy7BPIk", ExportName = "sceErrorDialogTerminate",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceErrorDialog")]
    public static int ErrorDialogTerminate(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "yXFLqVPxzJ4", ExportName = "sceImeDialogInit",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceImeDialog")]
    public static int ImeDialogInit(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "pR0FGi9pJig", ExportName = "sceImeDialogTerm",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceImeDialog")]
    public static int ImeDialogTerm(CpuContext ctx) => Ok(ctx);

    // ─── Network / NP basic ──────────────────────────────────────────────────

    [SysAbiExport(Nid = "3Fy7oHSekiM", ExportName = "sceNetCtlInit",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNetCtl")]
    public static int NetCtlInit(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "OWPwIBsD5io", ExportName = "sceNetCtlGetInfo",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNetCtl")]
    public static int NetCtlGetInfo(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "YPfHB+WA0S4", ExportName = "sceNpInit",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNp")]
    public static int NpInit(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "q0EZK7S0C6k", ExportName = "sceNpTerm",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNp")]
    public static int NpTerm(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "3W9SjcLB4Xk", ExportName = "sceNpCheckNpAvailability",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNp")]
    public static int NpCheckNpAvailability(CpuContext ctx) => Ok(ctx);

    // ─── NpManager ───────────────────────────────────────────────────────────

    [SysAbiExport(Nid = "Z8jHAQMVFSk", ExportName = "sceNpManagerInit",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpManager")]
    public static int NpManagerInit(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "DaBLIsgSaEw", ExportName = "sceNpManagerGetAccountIdA",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpManager")]
    public static int NpManagerGetAccountIdA(CpuContext ctx) => Ok(ctx);

    // ─── SaveData helpers ─────────────────────────────────────────────────────

    [SysAbiExport(Nid = "rMGAbw0Ods4", ExportName = "sceSaveDataInitialize3",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSaveData")]
    public static int SaveDataInitialize3(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "GnPPqLLtWog", ExportName = "sceSaveDataTerminate",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSaveData")]
    public static int SaveDataTerminate(CpuContext ctx) => Ok(ctx);

    // ─── PlayGo / Content streaming ──────────────────────────────────────────

    [SysAbiExport(Nid = "vc9B0oUFr5g", ExportName = "scePlayGoInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libScePlayGo")]
    public static int PlayGoInitialize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "DjKUs6PGHA8", ExportName = "scePlayGoTerminate",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libScePlayGo")]
    public static int PlayGoTerminate(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "zRlIHf0DXOY", ExportName = "scePlayGoOpen",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libScePlayGo")]
    public static int PlayGoOpen(CpuContext ctx) => OkWithHandle(ctx, CpuRegister.Rdi);

    [SysAbiExport(Nid = "BW3KUVS3ank", ExportName = "scePlayGoClose",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libScePlayGo")]
    public static int PlayGoClose(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "bUGEFJ6k0Bo", ExportName = "scePlayGoGetInstallSpeed",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libScePlayGo")]
    public static int PlayGoGetInstallSpeed(CpuContext ctx)
    {
        // Pretend the disc is fully installed at maximum speed.
        var outPtr = ctx[CpuRegister.Rdi];
        if (outPtr != 0)
        {
            Span<byte> speed = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(speed, uint.MaxValue);
            ctx.Memory.TryWrite(outPtr, speed);
        }

        return Ok(ctx);
    }

    // ─── System service helpers ───────────────────────────────────────────────

    [SysAbiExport(Nid = "fNYpkBN7gJM", ExportName = "sceSystemServiceParamGetInt",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemService")]
    public static int SystemServiceParamGetInt(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "GGNbBm8+J+Y", ExportName = "sceSystemServiceReceiveEvent",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemService")]
    public static int SystemServiceReceiveEvent(CpuContext ctx)
    {
        // 0x80020002 = SCE_KERNEL_ERROR_EWOULDBLOCK — no event queued,
        // which is correct and prevents spin-waits.
        ctx[CpuRegister.Rax] = unchecked((ulong)0x80020002);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    // ─── Trophy 2 (boot path) ─────────────────────────────────────────────────

    [SysAbiExport(Nid = "TBDwgxBaTCA", ExportName = "sceNpTrophy2CreateContext",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2CreateContext(CpuContext ctx) => OkWithHandle(ctx, CpuRegister.Rdi);

    [SysAbiExport(Nid = "kA4VPkVHBfk", ExportName = "sceNpTrophy2CreateHandle",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2CreateHandle(CpuContext ctx) => OkWithHandle(ctx, CpuRegister.Rdi);

    [SysAbiExport(Nid = "M1G5PLwGMHo", ExportName = "sceNpTrophy2RegisterContext",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2RegisterContext(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "FNkr2Tui0R0", ExportName = "sceNpTrophy2UnlockTrophy",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpTrophy2")]
    public static int NpTrophy2UnlockTrophy(CpuContext ctx) => Ok(ctx);

    // ─── Remote Play ──────────────────────────────────────────────────────────

    [SysAbiExport(Nid = "Rv4YcVbCrRo", ExportName = "sceRemoteplayInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceRemoteplay")]
    public static int RemoteplayInitialize(CpuContext ctx) => Ok(ctx);

    // ─── UserService ──────────────────────────────────────────────────────────

    [SysAbiExport(Nid = "6GAaVFBP2tM", ExportName = "sceUserServiceInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceUserService")]
    public static int UserServiceInitialize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "q2v+BVMWJNE", ExportName = "sceUserServiceGetInitialUser",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceUserService")]
    public static int UserServiceGetInitialUser(CpuContext ctx)
    {
        // Return userId = 1 (minimum valid PS5 user id).
        var outPtr = ctx[CpuRegister.Rdi];
        if (outPtr != 0)
        {
            Span<byte> uid = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(uid, 1);
            ctx.Memory.TryWrite(outPtr, uid);
        }

        return Ok(ctx);
    }

    [SysAbiExport(Nid = "j6YsGHCHSTE", ExportName = "sceUserServiceGetLoginUserIdList",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceUserService")]
    public static int UserServiceGetLoginUserIdList(CpuContext ctx) => Ok(ctx);

    // ─── Misc blocking stubs ──────────────────────────────────────────────────

    [SysAbiExport(Nid = "1WMvnQcCBP8", ExportName = "sceAppContentInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceAppContent")]
    public static int AppContentInitialize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "8Bh2MopBBpM", ExportName = "sceDiscMapInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceDiscMap")]
    public static int DiscMapInitialize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "XAVnKNmFeZ0", ExportName = "sceGameUpdateInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceGameUpdate")]
    public static int GameUpdateInitialize(CpuContext ctx) => Ok(ctx);

    [SysAbiExport(Nid = "Q7WT5WBMZEM", ExportName = "sceGameIntentInitialize",
        Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceNpGameIntent")]
    public static int GameIntentInitialize(CpuContext ctx) => Ok(ctx);
}
