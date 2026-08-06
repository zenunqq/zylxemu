// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE.Host.Sdl;

namespace ZylxEmu.HLE.Host.Posix;

internal sealed class PosixHostPlatform : IHostPlatform
{
    public IHostMemory Memory { get; } = new PosixHostMemory();

    public IHostThreading Threading { get; } = new PosixHostThreading();

    public IHostSymbolResolver Symbols { get; } = new PosixHostSymbolResolver();

    public IHostAudioOutput Audio { get; } = new SdlHostAudio();

    public IHostInput Input { get; } = new WindowHostInput();
}
