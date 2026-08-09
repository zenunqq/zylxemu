// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.GUI;
using Xunit;

namespace ZylxEmu.Libs.Tests.GUI;

public sealed class GameLibraryWatcherTests
{
    [Fact]
    public async Task Watch_FileCreation_RequestsRefresh()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"zylxemu-library-watcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var watcher = new GameLibraryWatcher(TimeSpan.FromMilliseconds(25));
            var refreshRequested = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.RefreshRequested += (_, _) => refreshRequested.TrySetResult();
            watcher.Watch([directory]);

            await File.WriteAllTextAsync(
                Path.Combine(directory, "eboot.bin"),
                "test");

            await refreshRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
