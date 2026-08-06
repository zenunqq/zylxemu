// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE.Host.Sdl;

namespace ZylxEmu.HLE.Host.Windows;

internal sealed class WindowsHostPlatform : IHostPlatform
{
    public IHostMemory Memory { get; } = new WindowsHostMemory();

    public IHostThreading Threading { get; } = new WindowsHostThreading();

    public IHostSymbolResolver Symbols { get; } = new WindowsHostSymbolResolver();

    public IHostAudioOutput Audio { get; } = new SdlHostAudio();

    public IHostInput Input { get; } = new WindowHostInput();
}
