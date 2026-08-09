// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using ZylxEmu.ShaderCompiler;

namespace ZylxEmu.ShaderCompiler.Vulkan;

// SPIR-V-specific shader artifact types. These stay beside the SPIR-V emitter (not in
// the backend-neutral ZylxEmu.ShaderCompiler project): each codegen owns its own
// compiled-shader shape.
public enum Gen5SpirvStage
{
    Vertex,
    Pixel,
    Compute,
}

public sealed record Gen5SpirvShader(
    byte[] Spirv,
    IReadOnlyList<Gen5GlobalMemoryBinding> GlobalMemoryBindings,
    IReadOnlyList<Gen5ImageBinding> ImageBindings,
    uint AttributeCount,
    IReadOnlyList<Gen5VertexInputBinding> VertexInputs);
