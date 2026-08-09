// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.Libs.Gpu.Vulkan;

/// <summary>The Vulkan backend's compiled shader: raw SPIR-V words.</summary>
internal sealed record VulkanCompiledGuestShader(byte[] Spirv) : IGuestCompiledShader
{
    public byte[] Payload => Spirv;

    public string PayloadFileExtension => "spv";
}
