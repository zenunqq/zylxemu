// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ZylxEmu.Libs.Gpu;

/// <summary>
/// Persistent on-disk cache for compiled SPIR-V / MSL shader bytecode.
/// Shader compilation is one of the largest contributors to boot-time stalls
/// and mid-game stutters. This cache maps a content-addressed key (xxHash3 of
/// GCN bytecode + compile-option fingerprint) to the already-translated host
/// bytecode so the translator is skipped on all subsequent runs.
///
/// Format (per entry):
///   [8]  key (xxHash3 of shader bytes XOR option fingerprint)
///   [4]  payload length (little-endian)
///   [N]  payload (SPIR-V bytes)
///   [4]  CRC32C checksum of payload (little-endian) — corruption guard
///
/// Thread safety: all public members are safe to call from any thread.
/// The backing dictionary is written once per new shader; subsequent reads
/// for the same key are lock-free.
/// </summary>
public sealed class ShaderBytecodeCache : IDisposable
{
    private const int MagicVersion = 0x5348_5243; // "SHRC"
    private const int HeaderSize = 8; // magic(4) + version(4)
    private const int EntryHeaderSize = 8 + 4; // key(8) + length(4)
    private const int EntryFooterSize = 4;     // CRC32C(4)

    private readonly string _path;
    private readonly ConcurrentDictionary<ulong, byte[]> _memory = new();
    private readonly object _writeLock = new();
    private long _hits;
    private long _misses;
    private long _evictions;

    // Environment-gated: set ZYLXEMU_SHADER_CACHE=0 to disable.
    public static readonly bool Enabled = !string.Equals(
        Environment.GetEnvironmentVariable("ZYLXEMU_SHADER_CACHE"),
        "0",
        StringComparison.Ordinal);

    private ShaderBytecodeCache(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Opens or creates the cache at <paramref name="path"/>.
    /// Returns a disabled no-op cache if <see cref="Enabled"/> is false.
    /// </summary>
    public static ShaderBytecodeCache Open(string path)
    {
        if (!Enabled)
        {
            return new ShaderBytecodeCache(path);
        }

        var cache = new ShaderBytecodeCache(path);
        cache.LoadFromDisk();
        return cache;
    }

    /// <summary>
    /// Builds the 64-bit lookup key for a shader.
    /// </summary>
    public static ulong MakeKey(ReadOnlySpan<byte> guestBytes, ulong optionFingerprint)
    {
        var hash = XxHash3.HashToUInt64(guestBytes);
        return hash ^ optionFingerprint;
    }

    /// <summary>
    /// Returns true and the cached host bytecode if the shader has been seen before.
    /// </summary>
    public bool TryGet(ulong key, out byte[]? hostBytecode)
    {
        if (!Enabled)
        {
            hostBytecode = null;
            return false;
        }

        if (_memory.TryGetValue(key, out hostBytecode))
        {
            Interlocked.Increment(ref _hits);
            return true;
        }

        Interlocked.Increment(ref _misses);
        return false;
    }

    /// <summary>
    /// Inserts translated host bytecode into the cache and persists it asynchronously.
    /// </summary>
    public void Put(ulong key, ReadOnlySpan<byte> hostBytecode)
    {
        if (!Enabled)
        {
            return;
        }

        var bytes = hostBytecode.ToArray();
        if (!_memory.TryAdd(key, bytes))
        {
            return; // already inserted by a racing thread
        }

        // Persist in the background to avoid blocking the render thread.
        ThreadPool.UnsafeQueueUserWorkItem(static state =>
        {
            var (self, k, b) = ((ShaderBytecodeCache, ulong, byte[]))state!;
            self.AppendEntry(k, b);
        }, (this, key, bytes));
    }

    public (long Hits, long Misses, long Entries) Stats() =>
        (Interlocked.Read(ref _hits),
         Interlocked.Read(ref _misses),
         _memory.Count);

    public void Dispose()
    {
        // Nothing to flush; entries are already written incrementally.
    }

    // ── Disk I/O ─────────────────────────────────────────────────────────────

    private void LoadFromDisk()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> header = stackalloc byte[HeaderSize];
            if (fs.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false) < HeaderSize)
            {
                return;
            }

            var magic = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (magic != MagicVersion)
            {
                Console.Error.WriteLine("[SHADER-CACHE] Stale or corrupt cache; starting fresh.");
                return;
            }

            Span<byte> entryHeader = stackalloc byte[EntryHeaderSize];
            var loaded = 0;
            while (true)
            {
                var read = fs.ReadAtLeast(entryHeader, EntryHeaderSize, throwOnEndOfStream: false);
                if (read < EntryHeaderSize)
                {
                    break;
                }

                var key = BinaryPrimitives.ReadUInt64LittleEndian(entryHeader);
                var length = BinaryPrimitives.ReadInt32LittleEndian(entryHeader[8..]);
                if (length <= 0 || length > 4 * 1024 * 1024)
                {
                    break; // corrupted entry length
                }

                var payload = new byte[length];
                if (fs.ReadAtLeast(payload, length, throwOnEndOfStream: false) < length)
                {
                    break;
                }

                Span<byte> crcBytes = stackalloc byte[EntryFooterSize];
                if (fs.ReadAtLeast(crcBytes, EntryFooterSize, throwOnEndOfStream: false) < EntryFooterSize)
                {
                    break;
                }

                var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(crcBytes);
                var computedCrc = Crc32C(payload);
                if (storedCrc != computedCrc)
                {
                    Interlocked.Increment(ref _evictions);
                    continue; // corrupt entry — skip
                }

                _memory.TryAdd(key, payload);
                loaded++;
            }

            if (loaded > 0)
            {
                Console.Error.WriteLine($"[SHADER-CACHE] Loaded {loaded} cached shaders from {_path}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SHADER-CACHE][WARN] Failed to load cache: {ex.Message}");
        }
    }

    private void AppendEntry(ulong key, byte[] payload)
    {
        try
        {
            lock (_writeLock)
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var isNew = !File.Exists(_path);
                using var fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (isNew)
                {
                    // Write file header.
                    Span<byte> header = stackalloc byte[HeaderSize];
                    BinaryPrimitives.WriteInt32LittleEndian(header, MagicVersion);
                    BinaryPrimitives.WriteInt32LittleEndian(header[4..], 1);
                    fs.Write(header);
                }
                else
                {
                    fs.Seek(0, SeekOrigin.End);
                }

                Span<byte> entryHeader = stackalloc byte[EntryHeaderSize];
                BinaryPrimitives.WriteUInt64LittleEndian(entryHeader, key);
                BinaryPrimitives.WriteInt32LittleEndian(entryHeader[8..], payload.Length);
                fs.Write(entryHeader);
                fs.Write(payload);

                Span<byte> crcBytes = stackalloc byte[EntryFooterSize];
                BinaryPrimitives.WriteUInt32LittleEndian(crcBytes, Crc32C(payload));
                fs.Write(crcBytes);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SHADER-CACHE][WARN] Failed to write entry: {ex.Message}");
        }
    }

    private static uint Crc32C(ReadOnlySpan<byte> data)
    {
        var crc = new Crc32();
        crc.Append(data);
        return BitConverter.ToUInt32(crc.GetCurrentHash());
    }
}
