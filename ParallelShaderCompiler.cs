// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;

namespace ZylxEmu.Libs.Gpu;

/// <summary>
/// Off-thread shader compiler queue. When the AGC encounters a new shader that
/// isn't in the bytecode cache, it can kick compilation to a background pool
/// thread and immediately submit a placeholder draw (skipped or depth-only)
/// while the real shader is being built. Once compiled the shader is stored
/// in <see cref="ShaderBytecodeCache"/> for instant retrieval on all future
/// uses — including the next game boot.
///
/// Degree of parallelism defaults to <c>max(1, logicalCores - 2)</c> so the
/// render thread and the game's main thread are never starved.
///
/// Usage:
///   1. <c>ParallelShaderCompiler.Instance.Enqueue(key, workFunc)</c> on the hot path.
///   2. Call <c>TryGetResult(key, out compiled)</c> in subsequent frames.
///   3. <c>WaitAll()</c> before capturing a pipeline-cache snapshot.
/// </summary>
public sealed class ParallelShaderCompiler : IDisposable
{
    public static readonly ParallelShaderCompiler Instance = new();

    // Log compilation events when ZYLXEMU_LOG_SHADER_COMPILE=1.
    private static readonly bool _trace = string.Equals(
        Environment.GetEnvironmentVariable("ZYLXEMU_LOG_SHADER_COMPILE"),
        "1", StringComparison.Ordinal);

    private readonly int _workerCount =
        Math.Max(1, Environment.ProcessorCount - 2);

    private readonly ConcurrentDictionary<ulong, CompileState> _pending = new();
    private readonly ConcurrentQueue<(ulong Key, Func<byte[]?> Work)> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread[] _workers;

    private long _compiled;
    private long _failed;
    private long _cacheHits;

    private enum CompileState { Pending, Done, Failed }

    private readonly ConcurrentDictionary<ulong, byte[]> _results = new();

    private ParallelShaderCompiler()
    {
        _workers = new Thread[_workerCount];
        for (var i = 0; i < _workerCount; i++)
        {
            var thread = new Thread(WorkerLoop)
            {
                Name = $"ZylxEmu ShaderCompile #{i}",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal, // never starve the render thread
            };
            _workers[i] = thread;
            thread.Start();
        }
    }

    /// <summary>
    /// Queue <paramref name="compileFunc"/> for background execution.
    /// If an entry for <paramref name="key"/> already exists the call is a no-op.
    /// </summary>
    public void Enqueue(ulong key, Func<byte[]?> compileFunc)
    {
        if (!_pending.TryAdd(key, CompileState.Pending))
        {
            return; // already queued or done
        }

        _queue.Enqueue((key, compileFunc));
        _signal.Release();
    }

    /// <summary>
    /// Returns true when compilation for <paramref name="key"/> finished successfully.
    /// </summary>
    public bool TryGetResult(ulong key, out byte[]? compiled)
    {
        if (_results.TryGetValue(key, out compiled))
        {
            Interlocked.Increment(ref _cacheHits);
            return true;
        }

        compiled = null;
        return false;
    }

    /// <summary>
    /// Returns true when a compile job for <paramref name="key"/> is still running.
    /// </summary>
    public bool IsPending(ulong key) =>
        _pending.TryGetValue(key, out var s) && s == CompileState.Pending;

    /// <summary>Block until the compile queue is empty (use before pipeline-cache snapshots).</summary>
    public void WaitAll(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (_queue.Count > 0 && sw.Elapsed < timeout)
        {
            Thread.Sleep(5);
        }
    }

    public (long Compiled, long Failed, long CacheHits, int Pending) Stats() =>
        (Interlocked.Read(ref _compiled),
         Interlocked.Read(ref _failed),
         Interlocked.Read(ref _cacheHits),
         _queue.Count);

    public void Dispose()
    {
        _cts.Cancel();
        _signal.Release(_workerCount);
        foreach (var t in _workers)
        {
            t.Join(500);
        }

        _cts.Dispose();
        _signal.Dispose();
    }

    // ── Worker ────────────────────────────────────────────────────────────────

    private void WorkerLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _signal.Wait(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_queue.TryDequeue(out var item))
            {
                continue;
            }

            var (key, work) = item;
            try
            {
                var sw = _trace ? Stopwatch.StartNew() : null;
                var result = work();
                if (result is not null)
                {
                    _results[key] = result;
                    _pending[key] = CompileState.Done;
                    Interlocked.Increment(ref _compiled);
                    if (_trace)
                    {
                        Console.Error.WriteLine(
                            $"[SHADER-COMPILE] key={key:X16} {result.Length}b " +
                            $"in {sw!.ElapsedMilliseconds}ms");
                    }
                }
                else
                {
                    _pending[key] = CompileState.Failed;
                    Interlocked.Increment(ref _failed);
                }
            }
            catch (Exception ex)
            {
                _pending[key] = CompileState.Failed;
                Interlocked.Increment(ref _failed);
                if (_trace)
                {
                    Console.Error.WriteLine($"[SHADER-COMPILE][ERROR] key={key:X16}: {ex.Message}");
                }
            }
        }
    }
}
