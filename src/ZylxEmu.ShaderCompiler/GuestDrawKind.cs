// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace ZylxEmu.ShaderCompiler;

/// <summary>
/// Guest draw patterns the decoder recognizes from known shader programs. Guest-domain,
/// backend-neutral — it previously lived inside the Vulkan presenter, which is exactly
/// the kind of placement this project exists to prevent.
/// </summary>
public enum GuestDrawKind
{
    None,
    FullscreenBarycentric,
}
