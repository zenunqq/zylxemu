// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Core;
using ZylxEmu.Core.Memory;
using ZylxEmu.HLE;

namespace ZylxEmu.Core.Loader;

public interface ISelfLoader
{
    SelfImage Load(ReadOnlySpan<byte> imageData, IVirtualMemory virtualMemory);

    SelfImage Load(ReadOnlySpan<byte> imageData, IVirtualMemory virtualMemory, IFileSystem? fs, string? mountRoot);

    SelfImage Load(ReadOnlySpan<byte> imageData, IVirtualMemory virtualMemory, IModuleManager moduleManager);

    SelfImage Load(ReadOnlySpan<byte> imageData, IVirtualMemory virtualMemory, IModuleManager moduleManager, IFileSystem? fs, string? mountRoot);

    SelfImage LoadAdditional(ReadOnlySpan<byte> imageData, IVirtualMemory virtualMemory, IModuleManager moduleManager, IFileSystem? fs, string? mountRoot);
}
