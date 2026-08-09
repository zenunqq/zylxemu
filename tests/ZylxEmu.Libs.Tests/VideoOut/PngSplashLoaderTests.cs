// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Libs.VideoOut;
using Xunit;

namespace ZylxEmu.Libs.Tests.VideoOut;

public sealed class PngSplashLoaderTests
{
    private const int IhdrCrcOffset = 29;
    private const int IdatCrcOffset = 53;
    private const int IendCrcOffset = 65;
    private const string ValidRgbPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGMQMgkDAAD4AJ3MaiF4AAAAAElFTkSuQmCC";

    [Theory]
    [InlineData(IhdrCrcOffset)]
    [InlineData(IdatCrcOffset)]
    [InlineData(IendCrcOffset)]
    public void TryLoadRejectsInvalidChunkCrc(int crcOffset)
    {
        var app0Root = Path.Combine(Path.GetTempPath(), $"zylxemu-png-{Guid.NewGuid():N}");
        var sceSysPath = Path.Combine(app0Root, "sce_sys");
        var pngPath = Path.Combine(sceSysPath, "pic0.png");
        var previousApp0Root = Environment.GetEnvironmentVariable("ZYLXEMU_APP0_DIR");

        Directory.CreateDirectory(sceSysPath);
        try
        {
            Environment.SetEnvironmentVariable("ZYLXEMU_APP0_DIR", app0Root);

            var png = Convert.FromBase64String(ValidRgbPng);
            File.WriteAllBytes(pngPath, png);

            Assert.True(PngSplashLoader.TryLoad(out var pixels, out var width, out var height));
            Assert.Equal(1U, width);
            Assert.Equal(1U, height);
            Assert.Equal([0x56, 0x34, 0x12, 0xFF], pixels);

            png[crcOffset] ^= 0x01;
            File.WriteAllBytes(pngPath, png);

            Assert.False(PngSplashLoader.TryLoad(out _, out _, out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ZYLXEMU_APP0_DIR", previousApp0Root);
            Directory.Delete(app0Root, recursive: true);
        }
    }
}
