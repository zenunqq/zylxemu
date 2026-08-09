// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Core.Cpu.Native;
using Xunit;

namespace ZylxEmu.Libs.Tests.Cpu;

public sealed unsafe class JitStubsTests
{
    [Fact]
    public void FindTlsAccessPatterns_IncludesLastValidOffset()
    {
        var pattern = JitStubs.TlsAccessPattern;
        var code = new byte[pattern.Length + 3];
        pattern.CopyTo(code.AsSpan(3));

        fixed (byte* codePointer = code)
        {
            var matches = JitStubs.FindTlsAccessPatterns(codePointer, code.Length);

            var match = Assert.Single(matches);
            Assert.Equal((nint)(codePointer + 3), match);
        }
    }
}
