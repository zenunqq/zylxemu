// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.Core.Runtime;

using ZylxEmu.Core.Cpu;
using ZylxEmu.Core.Cpu.Debugging;

public readonly struct ZylxEmuRuntimeOptions
{
    public CpuExecutionEngine CpuEngine { get; init; }

    public bool StrictDynlibResolution { get; init; }

    public int ImportTraceLimit { get; init; }

    /// <summary>
    /// An optional debugger to attach to guest execution. Flows through to
    /// <see cref="CpuExecutionOptions.DebugHook"/>. Null (the default) runs with
    /// no debugger attached.
    /// </summary>
    public ICpuDebugHook? DebugHook { get; init; }
}
