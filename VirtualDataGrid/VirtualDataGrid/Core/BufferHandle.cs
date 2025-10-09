using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// Reference-counted wrapper untuk buffer pooled.
    /// Pastikan buffer tidak di-return ke pool sampai semua pemakai selesai.
    /// </summary>
    public sealed class BufferHandle<T> where T : struct
    {
        private T[] _buffer;
        private int _length;
        private int _refCount;
        private readonly ArrayPool<T> _pool;

        public BufferHandle(T[] buffer, int length, ArrayPool<T> pool)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _length = length;
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _refCount = 1; // pemilik awal (converter/store)
        }

        public T[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(BufferHandle<T>));
        public ReadOnlyMemory<T> Memory => new ReadOnlyMemory<T>(_buffer, 0, _length);

        public void Retain()
        {
            if (_buffer == null) throw new ObjectDisposedException(nameof(BufferHandle<T>));
            Interlocked.Increment(ref _refCount);
        }

        public void Release(bool clearArray = true)
        {
            if (_buffer == null) return;
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                var buf = Interlocked.Exchange(ref _buffer, null);
                if (buf != null)
                    _pool.Return(buf, clearArray);
            }
        }

        public int RefCount => Volatile.Read(ref _refCount);
    }
}
