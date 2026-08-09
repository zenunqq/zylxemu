// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE;

namespace ZylxEmu.Libs.Ajm;

public static class AjmExports
{
    [SysAbiExport(
        Nid = "dl+4eHSzUu4",
        ExportName = "sceAjmInitialize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAjm")]
    public static int Initialize(CpuContext ctx) =>
        ZylxEmu.Libs.Audio.AjmExports.AjmInitialize(ctx);
}
