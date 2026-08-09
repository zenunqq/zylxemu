// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.Logging;

public interface IZylxEmuLogSink
{
    void Write(in LogEntry entry);
}
