// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.Core.Loader;
using ZylxEmu.HLE;

namespace ZylxEmu.Core.Runtime;

public interface IZylxEmuRuntime : IDisposable
{
    string? LastExecutionDiagnostics { get; }

    string? LastExecutionTrace { get; }

    string? LastSessionSummary { get; }

    string? LastBasicBlockTrace { get; }

    string? LastMilestoneLog { get; }

    SelfImage LoadImage(string ebootPath);

    OrbisGen2Result Run(string ebootPath);

    OrbisGen2Result DispatchHleCall(string nid, CpuContext context);
}
