// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.Core.Loader;

public readonly record struct ImportedSymbolRelocation(
    ulong TargetAddress,
    long Addend,
    string Nid,
    bool IsData);
