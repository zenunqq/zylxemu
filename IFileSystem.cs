// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.Core;

public interface IFileSystem
{
    bool Exists(string path);

    bool TryReadAllBytes(string path, out byte[] data);
}
