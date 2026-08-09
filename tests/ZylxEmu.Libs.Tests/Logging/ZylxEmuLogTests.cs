// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Logging;
using Xunit;

namespace ZylxEmu.Libs.Tests.Logging;

public sealed class ZylxEmuLogTests
{
    [Theory]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData(" Info ", LogLevel.Info)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("Error", LogLevel.Error)]
    [InlineData("critical", LogLevel.Critical)]
    [InlineData("None", LogLevel.None)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("fatal", LogLevel.Critical)]
    public void TryParseLevelAcceptsDefinedNamesAndAliases(string text, LogLevel expected)
    {
        Assert.True(ZylxEmuLog.TryParseLevel(text, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("999")]
    [InlineData("-1")]
    public void TryParseLevelRejectsInvalidValues(string? text)
    {
        Assert.False(ZylxEmuLog.TryParseLevel(text, out var level));
        Assert.Equal(default, level);
    }
}
