using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    // <summary>
    /// Pool string untuk deduplikasi dan optimasi memory
    /// 
    /// COCOK UNTUK:
    /// - Data dengan banyak string berulang (Buy/Sell, Category, Status, dll)
    /// - Kolom categorical dengan nilai terbatas
    /// - Optimasi memory untuk dataset besar
    /// </summary>
    public sealed class StringPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, int> _stringToId;
        private readonly ConcurrentDictionary<int, string> _idToString;
        private readonly StringComparer _comparer;
        private int _nextId;
        private long _totalLookups;
        private long _cacheHits;
        private bool _disposed;

        /// <summary>
        /// Shared instance (recommended for global use)
        /// </summary>
        public static readonly StringPool Shared = new();

        public StringPool(bool caseSensitive = false, int initialCapacity = 1024)
        {
            _comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

            _stringToId = new ConcurrentDictionary<string, int>(
                Environment.ProcessorCount,
                initialCapacity,
                _comparer
            );

            _idToString = new ConcurrentDictionary<int, string>(
                Environment.ProcessorCount,
                initialCapacity
            );

            _nextId = 0;
        }

        /// <summary>
        /// Dapatkan ID untuk string. Buat baru jika belum ada.
        /// 
        /// CONTOH:
        /// var id1 = pool.GetId("Buy");  // return 1
        /// var id2 = pool.GetId("BUY");  // return 1 (jika case-insensitive)
        /// var id3 = pool.GetId("Sell"); // return 2
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetId(string value)
        {
            if (string.IsNullOrEmpty(value))
                return -1;

            Interlocked.Increment(ref _totalLookups);

            if (_stringToId.TryGetValue(value, out int existingId))
            {
                Interlocked.Increment(ref _cacheHits);
                return existingId;
            }

            return _stringToId.GetOrAdd(value, AddNewString);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddNewString(string value)
        {
            int newId = Interlocked.Increment(ref _nextId);
            _idToString.TryAdd(newId, value);
            return newId;
        }

        /// <summary>
        /// Dapatkan string asli dari ID
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? GetString(int id)
        {
            if (id <= 0) return null;
            _idToString.TryGetValue(id, out var value);
            return value;
        }

        /// <summary>
        /// Clear all entries (careful: invalidates all references)
        /// </summary>
        public void Clear()
        {
            _stringToId.Clear();
            _idToString.Clear();
            _nextId = 0;
            _totalLookups = 0;
            _cacheHits = 0;
        }

        /// <summary>
        /// Statistik penggunaan pool
        /// </summary>
        public StringPoolStats GetStats()
        {
            long total = Interlocked.Read(ref _totalLookups);
            long hits = Interlocked.Read(ref _cacheHits);
            double hitRate = total > 0 ? (double)hits / total : 0;

            long avgLen = 0;
            int count = 0;
            foreach (var s in _stringToId.Keys)
            {
                avgLen += s?.Length ?? 0;
                count++;
            }
            avgLen = count > 0 ? avgLen / count : 0;

            long memSaved = EstimateMemorySaving(avgLen, total, count);

            return new StringPoolStats
            {
                UniqueStrings = count,
                TotalLookups = total,
                CacheHitRate = hitRate,
                EstimatedMemorySaved = memSaved
            };
        }

        private static long EstimateMemorySaving(long avgLen, long totalLookups, int uniqueCount)
        {
            // Rough estimation: assume avg string 20B overhead + 2B per char
            long raw = totalLookups * (avgLen * 2 + 20);
            long pooled = uniqueCount * (avgLen * 2 + 20) + totalLookups * 4;
            return Math.Max(0, raw - pooled);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Clear();
            _disposed = true;
        }
    }

    public readonly struct StringPoolStats
    {
        public int UniqueStrings { get; init; }
        public long TotalLookups { get; init; }
        public double CacheHitRate { get; init; }
        public long EstimatedMemorySaved { get; init; }

        public override string ToString()
        {
            return $"Strings={UniqueStrings}, Lookups={TotalLookups}, HitRate={CacheHitRate:P1}, Saved={EstimatedMemorySaved / 1024.0:N1} KB";
        }
    }
}