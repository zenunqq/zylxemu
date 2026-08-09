// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Libs.VideoOut;
using Xunit;

namespace ZylxEmu.Libs.Tests.VideoOut;

public sealed class HostVideoOptionsTests
{
    [Fact]
    public void NormalizeClampsUnsafeHostValues()
    {
        var options = new HostVideoOptions
        {
            Width = 1,
            Height = 99_999,
            DisplayIndex = -2,
            RefreshRate = 5000,
            HdrMode = (HostHdrMode)99,
        }.Normalize();

        Assert.Equal(640, options.Width);
        Assert.Equal(16384, options.Height);
        Assert.Equal(0, options.DisplayIndex);
        Assert.Equal(1000, options.RefreshRate);
        Assert.Equal(HostHdrMode.Auto, options.HdrMode);
    }
}
