using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Data
{
    /// <summary>
    ///  hybrid data store for high-performance data grids.
    /// - Array-based storage with O(1) access
    /// - ReaderWriterLockSlim for optimal read concurrency  
    /// - Manual memory management for InternalRow buffers
    /// - Batch operations with minimal locking
    /// - Zero-copy spans for internal processing
    /// - Safe snapshots for UI rendering
    /// - Swap-last removal for O(1) deletes
    /// </summary>
    public sealed class HybridDataStore : IDisposable
    {
        private InternalRow[] _data;
        private int _count;
        private readonly Dictionary<long, int> _idToIndex;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private const int DefaultCapacity = 4096; // Optimized for L1 cache
        private bool _disposed;

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try { return _count; }
                finally { _lock.ExitReadLock(); }
            }
        }

        public int Capacity => _data.Length;

        public HybridDataStore(int initialCapacity = DefaultCapacity)
        {
            initialCapacity = Math.Max(DefaultCapacity, initialCapacity);
            _data = new InternalRow[initialCapacity];
            _idToIndex = new Dictionary<long, int>(initialCapacity);
        }

        #region Core Operations
        /// <summary>
        /// Add or update single row with manual memory management
        /// </summary>
        public void AddOrUpdate(InternalRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                if (_idToIndex.TryGetValue(row.Id, out int index))
                {
                    // UPDATE: Release old buffer, keep new
                    _data[index].ReleaseHandle();
                    _data[index] = row;
                }
                else
                {
                    // ADD: Ensure capacity and insert
                    EnsureCapacity(_count + 1);
                    _data[_count] = row;
                    _idToIndex[row.Id] = _count;
                    _count++;
                }
            }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// High-performance batch operation with single lock acquisition
        /// </summary>
        public void AddOrUpdateBatch(ReadOnlySpan<InternalRow> rows)
        {
            if (rows.IsEmpty) return;
            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                EnsureCapacity(_count + rows.Length);

                foreach (var row in rows)
                {
                    if (_idToIndex.TryGetValue(row.Id, out int index))
                    {
                        _data[index].ReleaseHandle();
                        _data[index] = row;
                    }
                    else
                    {
                        _data[_count] = row;
                        _idToIndex[row.Id] = _count;
                        _count++;
                    }
                }
            }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// Remove by id with O(1) swap-last technique
        /// </summary>
        public bool Remove(long id)
        {
            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                if (!_idToIndex.TryGetValue(id, out int index))
                    return false;

                // Release buffer of removed row
                _data[index].ReleaseHandle();

                // Swap with last element for O(1) removal
                int lastIndex = _count - 1;
                if (index < lastIndex)
                {
                    _data[index] = _data[lastIndex];
                    _idToIndex[_data[index].Id] = index;
                }

                // Clear last element and update count
                _data[lastIndex] = default;
                _idToIndex.Remove(id);
                _count--;

                // Optional: trim if significantly empty
                if (_count < _data.Length / 4 && _data.Length > DefaultCapacity * 2)
                    TrimCapacity(_data.Length / 2);

                return true;
            }
            finally { _lock.ExitWriteLock(); }
        }
        #endregion

        #region Read Operations
        /// <summary>
        /// Safe snapshot for UI rendering - returns new array copy
        /// </summary>
        public InternalRow[] GetSafeSnapshot()
        {
            ThrowIfDisposed();

            _lock.EnterReadLock();
            try
            {
                var snapshot = new InternalRow[_count];
                Array.Copy(_data, snapshot, _count);
                return snapshot;
            }
            finally { _lock.ExitReadLock(); }
        }

        /// <summary>
        /// Virtualized viewport snapshot for efficient rendering
        /// </summary>
        public InternalRow[] GetViewportSnapshot(int startIndex, int count)
        {
            ThrowIfDisposed();

            _lock.EnterReadLock();
            try
            {
                startIndex = Math.Max(0, startIndex);
                count = Math.Min(count, _count - startIndex);

                if (count <= 0)
                    return Array.Empty<InternalRow>();

                var viewport = new InternalRow[count];
                Array.Copy(_data, startIndex, viewport, 0, count);
                return viewport;
            }
            finally { _lock.ExitReadLock(); }
        }

        /// <summary>
        /// High-performance zero-copy access for internal processing
        /// Caller MUST call releaseLock action after use
        /// </summary>
        public ReadOnlyMemory<InternalRow> SnapshotAll()
        {
            _lock.EnterReadLock();
            try
            {
                if (_count == 0) return ReadOnlyMemory<InternalRow>.Empty;
                var copy = new InternalRow[_count];
                Array.Copy(_data, copy, _count);
                return copy;
            }
            finally { _lock.ExitReadLock(); }
        }

        // ✅ HIGH-PERFORMANCE (untuk advanced use)
        public ReadOnlySpan<InternalRow> GetLiveSpan(out IDisposable lockToken)
        {
            _lock.EnterReadLock();
            lockToken = new ReadLockToken(_lock);
            return new ReadOnlySpan<InternalRow>(_data, 0, _count);
        }
        /// <summary>
        /// Try-get pattern for single row access
        /// </summary>
        public bool TryGetValue(long id, out InternalRow row)
        {
            ThrowIfDisposed();

            _lock.EnterReadLock();
            try
            {
                if (_idToIndex.TryGetValue(id, out int index))
                {
                    row = _data[index];
                    return true;
                }
                row = default;
                return false;
            }
            finally { _lock.ExitReadLock(); }
        }
        #endregion

        #region Bulk Operations
        /// <summary>
        /// Replace entire dataset in single operation
        /// </summary>
        public void ReplaceAll(ReadOnlySpan<InternalRow> newData)
        {
            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                // Release all existing buffers
                for (int i = 0; i < _count; i++)
                    _data[i].ReleaseHandle();

                // Reset and repopulate
                _count = 0;
                _idToIndex.Clear();

                EnsureCapacity(newData.Length);
                foreach (var row in newData)
                {
                    _data[_count] = row;
                    _idToIndex[row.Id] = _count;
                    _count++;
                }
            }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// Clear all data and return to initial capacity
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                for (int i = 0; i < _count; i++)
                    _data[i].ReleaseHandle();

                Array.Clear(_data, 0, _count);
                _idToIndex.Clear();
                _count = 0;

                // Reset to reasonable capacity if overly large
                if (_data.Length > DefaultCapacity * 8)
                    Array.Resize(ref _data, DefaultCapacity * 2);
            }
            finally { _lock.ExitWriteLock(); }
        }
        #endregion

        #region Memory Management
        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _data.Length)
                return;

            int newCapacity = Math.Max(_data.Length * 2, requiredCapacity);
            var newArray = new InternalRow[newCapacity];
            Array.Copy(_data, newArray, _count);
            _data = newArray;
        }

        private void TrimCapacity(int newCapacity)
        {
            if (newCapacity >= _count && newCapacity < _data.Length)
            {
                Array.Resize(ref _data, newCapacity);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HybridDataStore));
        }
        #endregion

        #region Disposable Pattern
        public void Dispose()
        {
            if (_disposed) return;

            _lock.EnterWriteLock();
            try
            {
                for (int i = 0; i < _count; i++)
                {
                    try { _data[i].ReleaseHandle(); }
                    catch { /* Swallow disposal exceptions */ }
                }

                _data = Array.Empty<InternalRow>();
                _idToIndex.Clear();
                _count = 0;
                _disposed = true;
            }
            finally
            {
                _lock.ExitWriteLock();
                _lock.Dispose();
            }
        }
        #endregion

        #region Helper Types
        private sealed class ReadLockToken : IDisposable
        {
            private ReaderWriterLockSlim _lock;

            public ReadLockToken(ReaderWriterLockSlim lockObj)
            {
                _lock = lockObj;
            }

            public void Dispose()
            {
                _lock?.ExitReadLock();
                _lock = null;
            }
        }
        #endregion
    }
}