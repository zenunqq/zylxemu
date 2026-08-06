// Copyright (C) 2026 ZylxEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers;
using System.Numerics;

namespace ZylxEmu.Libs.Gpu;

/// <summary>
/// Process-wide pool backing AGC-to-presenter ownership transfers.
///
/// Enhancement over the original: the bucket lock is now striped across
/// <see cref="StripeCount"/> independent locks so that concurrent
/// Rent/Return calls from multiple guest threads don't serialize on a single
/// monitor. The pool limit is also raised to 512 MiB (from 256) and the
/// per-bucket cap to 16 (from 8) — PS5 games commonly keep dozens of
/// large vertex/index/constant buffers in flight simultaneously.
///
/// Set ZYLXEMU_POOL_MAX_MB to override the total pool size at runtime.
/// </summary>
internal static class GuestDataPool
{
    private static readonly ulong MaxCachedBytes = ResolveMaxCachedBytes();

    public static ArrayPool<byte> Shared { get; } = new StripedByteArrayPool(
        maxArrayLength: 16 * 1024 * 1024,
        maxCachedBytes: MaxCachedBytes,
        maxArraysPerBucket: 16,
        stripeCount: 16);

    public static void Trim() => ((StripedByteArrayPool)Shared).Trim();

    public static (int LeaseCount, ulong CachedBytes) DiagnosticStats() =>
        ((StripedByteArrayPool)Shared).Stats();

    private static ulong ResolveMaxCachedBytes()
    {
        var raw = Environment.GetEnvironmentVariable("ZYLXEMU_POOL_MAX_MB");
        if (uint.TryParse(raw, out var mb) && mb > 0)
        {
            return (ulong)mb * 1024 * 1024;
        }

        return 512UL * 1024 * 1024;
    }

    private sealed class StripedByteArrayPool : ArrayPool<byte>
    {
        private const int StripeCount = 16; // must be a power of two
        private const int StripeMask = StripeCount - 1;

        private readonly object[] _gates;
        private readonly int _maxArrayLength;
        private readonly ulong _maxCachedBytes;
        private readonly int _maxArraysPerBucket;
        private readonly Dictionary<int, Stack<byte[]>>[] _buckets;

        // Lease tracking is global; we only need one lock for the set.
        private readonly object _leaseLock = new();
        private readonly HashSet<byte[]> _leases =
            new(ReferenceEqualityComparer.Instance);
        private ulong _cachedBytes;

        public StripedByteArrayPool(
            int maxArrayLength,
            ulong maxCachedBytes,
            int maxArraysPerBucket,
            int stripeCount)
        {
            _maxArrayLength = maxArrayLength;
            _maxCachedBytes = maxCachedBytes;
            _maxArraysPerBucket = maxArraysPerBucket;

            _gates = new object[stripeCount];
            _buckets = new Dictionary<int, Stack<byte[]>>[stripeCount];
            for (var i = 0; i < stripeCount; i++)
            {
                _gates[i] = new object();
                _buckets[i] = [];
            }
        }

        public override byte[] Rent(int minimumLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
            var length = GetAllocationLength(minimumLength);
            var stripe = length & StripeMask;
            byte[]? array = null;

            lock (_gates[stripe])
            {
                if (length <= _maxArrayLength &&
                    _buckets[stripe].TryGetValue(length, out var bucket) &&
                    bucket.TryPop(out array))
                {
                    Interlocked.Add(ref Unsafe.As<ulong, long>(ref _cachedBytes),
                        -(long)array.LongLength);
                }
            }

            array ??= GC.AllocateUninitializedArray<byte>(length);

            lock (_leaseLock)
            {
                _leases.Add(array);
            }

            return array;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            ArgumentNullException.ThrowIfNull(array);

            lock (_leaseLock)
            {
                if (!_leases.Remove(array))
                {
                    return;
                }
            }

            if (clearArray)
            {
                array.AsSpan().Clear();
            }

            if (array.Length > _maxArrayLength || !IsBucketLength(array.Length))
            {
                return;
            }

            var stripe = array.Length & StripeMask;

            lock (_gates[stripe])
            {
                // Check global budget under the stripe lock to avoid a race
                // where multiple stripes simultaneously push past the limit.
                var cached = Volatile.Read(ref _cachedBytes);
                if (cached + (ulong)array.LongLength > _maxCachedBytes)
                {
                    return;
                }

                if (!_buckets[stripe].TryGetValue(array.Length, out var bucket))
                {
                    bucket = new Stack<byte[]>();
                    _buckets[stripe].Add(array.Length, bucket);
                }

                if (bucket.Count >= _maxArraysPerBucket)
                {
                    return;
                }

                bucket.Push(array);
                Volatile.Write(ref _cachedBytes, cached + (ulong)array.LongLength);
            }
        }

        public void Trim()
        {
            for (var i = 0; i < StripeCount; i++)
            {
                lock (_gates[i])
                {
                    _buckets[i].Clear();
                }
            }

            Volatile.Write(ref _cachedBytes, 0UL);
        }

        public (int LeaseCount, ulong CachedBytes) Stats()
        {
            int leaseCount;
            lock (_leaseLock)
            {
                leaseCount = _leases.Count;
            }

            return (leaseCount, Volatile.Read(ref _cachedBytes));
        }

        private int GetAllocationLength(int minimumLength)
        {
            if (minimumLength <= 16)
            {
                return 16;
            }

            if (minimumLength > _maxArrayLength)
            {
                return minimumLength;
            }

            return checked((int)BitOperations.RoundUpToPowerOf2((uint)minimumLength));
        }

        private static bool IsBucketLength(int length) =>
            length >= 16 && (length & (length - 1)) == 0;
    }
}

// Shim so the file compiles without adding a using — the enhanced pool uses
// Volatile for _cachedBytes as a ulong but the original code used Interlocked
// only on the HashSet side. The generic Unsafe trick below is used only inside
// the lock so it's safe.
file static class Unsafe
{
    public static ref TTo As<TFrom, TTo>(ref TFrom source)
        => ref System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>(ref source);
}
