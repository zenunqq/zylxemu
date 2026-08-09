# ZylxEmu — Performance & Compatibility Enhancements

This document describes every change made on top of the upstream ZylxEmu source.
All new files are in `src/ZylxEmu.Libs/` unless noted otherwise.

---

## 1. Persistent Shader Bytecode Cache (`Gpu/ShaderBytecodeCache.cs`)

**What it does**  
The first time a game encounters a new GCN shader it must compile it to SPIR-V.
This cache saves the compiled bytes to disk under
`user/pipeline_cache/<TitleId>/shader-bytecode.bin`.
On the next boot (or next time that shader is needed) the compiler is skipped entirely.

**Impact**  
- Eliminates shader-stutter micro-freezes mid-game once shaders have been seen once.
- Dramatically reduces per-game boot time on second and subsequent launches.

**Environment variables**  
| Variable | Default | Effect |
|---|---|---|
| `ZYLXEMU_SHADER_CACHE` | `1` | Set to `0` to disable |

---

## 2. Parallel Background Shader Compiler (`Gpu/ParallelShaderCompiler.cs`)

**What it does**  
When a new shader is encountered, compilation is offloaded to a worker pool
instead of blocking the render thread. The render thread submits a depth-only
placeholder draw while the real shader compiles; once the worker finishes the
result lands in `ShaderBytecodeCache` for instant reuse.

Worker count = `max(1, CPU cores − 2)` so the render and game threads are
never starved.

**Impact**  
- First-time shader hitches reduced from frame-blocking to sub-frame.
- Workers run at `BelowNormal` priority — no FPS regression during heavy load.

**Environment variables**  
| Variable | Default | Effect |
|---|---|---|
| `ZYLXEMU_LOG_SHADER_COMPILE` | `0` | Set to `1` for per-shader compile log |

---

## 3. Striped GuestDataPool (`Gpu/GuestDataPool.cs`)

**What it does**  
The AGC-to-presenter buffer pool previously used a single lock for all Rent/Return
operations. With multiple guest CPU threads submitting draws simultaneously that
single lock becomes a serialization bottleneck.

The enhanced version stripes the bucket dictionary across **16 independent locks**
(one per power-of-two bucket size class). Threads with different buffer sizes
never contend.

Additional changes:
- Pool ceiling raised from **256 MiB → 512 MiB** (override with `ZYLXEMU_POOL_MAX_MB`).
- Per-bucket array cap raised from 8 → 16.
- `GC.AllocateUninitializedArray` used for Rent to skip zero-initialization overhead.

**Impact**  
- Reduces CPU contention under multi-threaded draw submission.
- Fewer large allocations reaching the GC.

---

## 4. Expanded Boot-Compat Stubs (`Stubs/BootCompatStubs.cs`)

**What it does**  
Adds success stubs for ~25 additional PS5 system calls across these libraries:
- `libSceErrorDialog` — ErrorDialog init/term
- `libSceImeDialog` — keyboard dialog
- `libSceNetCtl` / `libSceNp` — network and NP base init
- `libSceNpManager` — account ID queries
- `libSceSaveData` — Initialize3 / Terminate
- `libScePlayGo` — streaming, install-speed (reports fully installed)
- `libSceSystemService` — ParamGetInt, ReceiveEvent (returns EWOULDBLOCK)
- `libSceNpTrophy2` — Create/Register/Unlock (no-ops)
- `libSceRemoteplay` — Initialize
- `libSceUserService` — Initialize, GetInitialUser (returns userId=1), GetLoginUserIdList
- `libSceAppContent`, `libSceDiscMap`, `libSceGameUpdate`, `libSceNpGameIntent` — Initialize

**Impact**  
- Games that previously hit a black screen because an unresolved NID made their
  init code bail out now continue past that point.
- Particularly helps titles that check NP availability before showing their main menu.

---

## 5. Thread Affinity Manager (`ThreadAffinityManager.cs`)

**What it does**  
When `ZYLXEMU_THREAD_AFFINITY=1` is set, the emulator pins threads to specific
logical cores mirroring the PS5's Zen 2 allocation:

| Role | Cores |
|---|---|
| Render (Vulkan record) | 0 |
| Audio / NGS2 | 1 |
| Guest CPU threads | 2–5 |
| Shader compile workers | 6–7 |
| Background I/O | last core |

Falls back to no-op on hosts with < 4 logical processors.

**Impact**  
- Reduces LLC cache thrashing between render and game threads.
- Eliminates scheduler migration latency spikes on NUMA or hybrid-core (P+E) CPUs.

---

## 6. Adaptive HostTiming (`HostTiming.cs`)

**What it does**  
The original timing code hard-coded a 1.2 ms scheduler overshoot threshold.
On hosts with MMCSS (Windows multimedia mode) or RT kernels the actual overshoot
is < 0.3 ms; on default Windows the overshoot can be 2–4 ms.

The enhanced version **calibrates the threshold at startup** by measuring 10
`Thread.Sleep(1)` samples and computing the average overshoot. The result is used
as the coarse/fine sleep crossover point.

Skip calibration with `ZYLXEMU_SKIP_TIMING_CALIBRATION=1`.

**Impact**  
- Better frame pacing on all host OSes without manual tuning.
- Prevents the coarse sleep from overshooting and doubling frame time.

---

## 7. Expanded PerfOverlay (`VideoOut/PerfOverlay.cs`)

**What it does**  
Adds a sixth HUD line showing:
- Shader cache: hit count, miss/compile count, pending queue depth
- Buffer pool: cached bytes, active lease count

Panel height expanded from 176 → 220 px to accommodate the new line.

---

## 8. Frame Cap + Precise Pacing (`VideoOut/HostVideoOptions.cs`)

**What it does**  
Adds two new `HostVideoOptions` fields:

| Field | Default | Description |
|---|---|---|
| `MaxFps` | 0 (unlimited) | Hard frame-rate cap. Override with `ZYLXEMU_MAX_FPS`. |
| `PreciseFramePacing` | `false` | Busy-wait after each frame to hit MaxFps precisely. |

**Impact**  
- `MaxFps=60` prevents GPU overclocking artifacts on titles that run uncapped.
- `PreciseFramePacing=true` gives sub-millisecond frame delivery on hosts where
  VSync alone isn't available or is unreliable.

---

## 9. Memory Budget Override (`Kernel/KernelMemoryCompatExports.cs`)

**What it does**  
The flexible memory pool was hard-coded to 448 MiB, which is well below the PS5's
actual ~5.25 GiB flexible allocation limit. Games that try to allocate larger pools
get `ENOMEM` and often abort their init.

Enhanced version:
- Flexible memory ceiling raised to **5376 MiB** (5.25 GiB, matching PS5 spec).
- Both limits can be overridden at runtime:

| Variable | Default |
|---|---|
| `ZYLXEMU_DIRECT_MEM_MB` | 16384 |
| `ZYLXEMU_FLEX_MEM_MB` | 5376 |

**Impact**  
- Fixes boot failures in games that probe memory availability during init.
- Users with < 16 GiB host RAM can reduce `ZYLXEMU_DIRECT_MEM_MB` to avoid OOM.

---

## Environment Variable Reference

| Variable | Default | Description |
|---|---|---|
| `ZYLXEMU_SHADER_CACHE` | `1` | `0` = disable bytecode cache |
| `ZYLXEMU_LOG_SHADER_COMPILE` | `0` | `1` = log each shader compile |
| `ZYLXEMU_POOL_MAX_MB` | `512` | Buffer pool ceiling (MiB) |
| `ZYLXEMU_THREAD_AFFINITY` | `0` | `1` = pin threads to cores |
| `ZYLXEMU_SKIP_TIMING_CALIBRATION` | `0` | `1` = use fixed 1.2 ms threshold |
| `ZYLXEMU_MAX_FPS` | `0` | Max rendered FPS (0 = unlimited) |
| `ZYLXEMU_DIRECT_MEM_MB` | `16384` | Direct GPU memory pool (MiB) |
| `ZYLXEMU_FLEX_MEM_MB` | `5376` | Flexible memory pool (MiB) |
| `ZYLXEMU_GPU_BACKEND` | `vulkan` | `vulkan` or `metal` (macOS) |
| `ZYLXEMU_OVERLAY` | `1` | `0` = hide perf HUD on start |
| `ZYLXEMU_PROFILE_RENDER` | `0` | `1` = enable render phase profiler |
