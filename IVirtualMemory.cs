// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Core.Loader;
using ZylxEmu.HLE;

namespace ZylxEmu.Core.Memory;

public interface IVirtualMemory : ICpuMemory
{
    void Clear();

    void Map(ulong virtualAddress, ulong memorySize, ulong fileOffset, ReadOnlySpan<byte> fileData, ProgramHeaderFlags protection);

    IReadOnlyList<VirtualMemoryRegion> SnapshotRegions();
}
