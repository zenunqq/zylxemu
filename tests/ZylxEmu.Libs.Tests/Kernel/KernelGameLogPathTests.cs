// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Libs.Kernel;
using Xunit;

namespace ZylxEmu.Libs.Tests.Kernel;

[Collection(KernelMemoryCompatStateCollection.Name)]
public sealed class KernelGameLogPathTests : IDisposable
{
    private readonly string? _previousHostappRoot =
        Environment.GetEnvironmentVariable("ZYLXEMU_HOSTAPP_DIR");
    private readonly string? _previousDevlogRoot =
        Environment.GetEnvironmentVariable("ZYLXEMU_DEVLOG_APP_DIR");

    public KernelGameLogPathTests()
    {
        Environment.SetEnvironmentVariable("ZYLXEMU_HOSTAPP_DIR", null);
        Environment.SetEnvironmentVariable("ZYLXEMU_DEVLOG_APP_DIR", null);
        KernelMemoryCompatExports.ConfigureApplicationInfo("ppsa/01:342");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ZYLXEMU_HOSTAPP_DIR", _previousHostappRoot);
        Environment.SetEnvironmentVariable("ZYLXEMU_DEVLOG_APP_DIR", _previousDevlogRoot);
        KernelMemoryCompatExports.ConfigureApplicationInfo(null);

        var testRoot = Path.Combine(
            AppContext.BaseDirectory,
            "user",
            "game_logs",
            "PPSA_01_342");
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void HostappUsesPerTitleGameLogDirectory()
    {
        var path = KernelMemoryCompatExports.ResolveGuestPath("/hostapp/logs/game.log");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "user",
                "game_logs",
                "PPSA_01_342",
                "hostapp",
                "logs",
                "game.log")),
            path);
    }

    [Fact]
    public void DevlogUsesPerTitleGameLogDirectory()
    {
        var path = KernelMemoryCompatExports.ResolveGuestPath("/devlog/app/debug.log");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "user",
                "game_logs",
                "PPSA_01_342",
                "devlog",
                "app",
                "debug.log")),
            path);
    }

    [Fact]
    public void ExplicitHostappOverrideIsPreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), "ZylxEmuTests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("ZYLXEMU_HOSTAPP_DIR", root);

            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), "logs", "game.log"),
                KernelMemoryCompatExports.ResolveGuestPath("/hostapp/logs/game.log"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ZYLXEMU_HOSTAPP_DIR", null);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
