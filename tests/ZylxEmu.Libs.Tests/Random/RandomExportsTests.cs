// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.HLE;
using ZylxEmu.Libs.Random;
using Xunit;

namespace ZylxEmu.Libs.Tests.Random;

public sealed class RandomExportsTests
{
    private const ulong BaseAddress = 0x1000;
    private const int RandomErrorInvalid = unchecked((int)0x817C0016);

    [Fact]
    public void GetRandomNumberWritesRequestedBytes()
    {
        var memory = new FakeCpuMemory(BaseAddress, 64);
        var ctx = CreateContext(memory, BaseAddress, 64);

        Assert.Equal(0, RandomExports.RandomGetRandomNumber(ctx));

        var bytes = new byte[64];
        Assert.True(memory.TryRead(BaseAddress, bytes));
        Assert.NotEqual(new byte[64], bytes);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(BaseAddress, 65)]
    public void GetRandomNumberRejectsInvalidArguments(ulong address, ulong size)
    {
        var memory = new FakeCpuMemory(BaseAddress, 64);
        var ctx = CreateContext(memory, address, size);

        Assert.Equal(RandomErrorInvalid, RandomExports.RandomGetRandomNumber(ctx));
    }

    [Fact]
    public void GetRandomNumberAcceptsEmptyRequest()
    {
        var memory = new FakeCpuMemory(BaseAddress, 64);
        var ctx = CreateContext(memory, 0, 0);

        Assert.Equal(0, RandomExports.RandomGetRandomNumber(ctx));
    }

    [Fact]
    public void GetRandomNumberReportsUnmappedDestination()
    {
        var memory = new FakeCpuMemory(BaseAddress, 64);
        var ctx = CreateContext(memory, BaseAddress + 64, 1);

        Assert.Equal(
            (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT,
            RandomExports.RandomGetRandomNumber(ctx));
    }

    private static CpuContext CreateContext(
        ICpuMemory memory,
        ulong destination,
        ulong size)
    {
        var ctx = new CpuContext(memory, Generation.Gen5);
        ctx[CpuRegister.Rdi] = destination;
        ctx[CpuRegister.Rsi] = size;
        return ctx;
    }
}
