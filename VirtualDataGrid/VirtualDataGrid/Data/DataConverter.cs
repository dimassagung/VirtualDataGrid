using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using VirtualDataGrid.Controls;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Data
{
   // Entity(Order/Trade/Stock)
   //     │
   //     ▼
   //DataConverter
   //     │ (compile accessor)
   //     ▼
   //InternalRow
   //     │
   //     └─ Cells[] : CellValue[]
   //            │
   //            ├─ double (NumericValue)
   //            ├─ int (StringId) → StringPool untuk text
   //            └─ bool (BoolValue)

    /// <summary>
    /// Converter generic dari entity T -> InternalRow
    /// - Gunakan precompiled property getters (fast, no reflection setiap kali)
    /// - ArrayPool untuk CellValue[] agar hemat GC
    /// - Mendukung parallel bulk conversion
    /// </summary>
    public sealed class DataConverter<T> : IDisposable where T : class
    {
        private readonly Func<T, object?>[] _propertyGetters;
        private readonly string[] _bindingPaths;
        private readonly ArrayPool<CellValue> _pool = ArrayPool<CellValue>.Shared;
        private readonly int _columnCount;
        private readonly StringPool? _stringPool;
        private bool _disposed;
        private readonly ColumnCollection _columns;
        //public DataConverter(ColumnCollection columns)
        //{
        //    _columnMap = new ColumnMap(columns.Select(c => c.BindingPath).ToArray());
        //}
        public DataConverter(ColumnCollection columns)
        //public DataConverter(ColumnCollection columns)
        {
            if (columns == null || columns.Count == 0)
                return;
                //throw new ArgumentException("Columns cannot be null or empty");

            _columns = columns;
            _columnCount = _columns.Count;
            _propertyGetters = new Func<T, object?>[_columnCount];
          
            PrecompileGetters();
        }

        //public DataConverter(IEnumerable<string> bindingPaths)
        //{
        //    _bindingPaths = bindingPaths?.ToArray() ?? throw new ArgumentNullException(nameof(bindingPaths));
        //    _columnCount = _bindingPaths.Length;
        //    if (_columnCount == 0) throw new ArgumentException("bindingPaths must contain at least one entry");
        //    _propertyGetters = new Func<T, object?>[_columnCount];

        //    PrecompileGetters();
        //}

        private void PrecompileGetters()
        {
            for (int i = 0; i < _columnCount; i++)
            {
                _propertyGetters[i] = CompileGetter(_bindingPaths[i]);
            }
        }

        private Func<T, object?> CompileGetter(string propertyPath)
        {
            // Support nested path "A.B.C"
            var param = Expression.Parameter(typeof(T), "x");
            Expression body = param;
            foreach (var part in propertyPath.Split('.'))
            {
                var pi = body.Type.GetProperty(part, BindingFlags.Instance | BindingFlags.Public);
                if (pi == null)
                {
                    // fallback to return null if property missing
                    return _ => null;
                }
                body = Expression.Property(body, pi);
            }
            var convert = Expression.Convert(body, typeof(object));
            return Expression.Lambda<Func<T, object?>>(convert, param).Compile();
        }

        /// <summary>
        /// Convert a collection of T into InternalRow[].
        /// Uses parallelism for large inputs.
        /// Caller must return pooled arrays via ReturnCellsBatch(...) when appropriate.
        /// </summary>
        public InternalRow[] ConvertToInternal(IList<T> items)
        {
            if (items == null || items.Count == 0) return Array.Empty<InternalRow>();

            var result = new InternalRow[items.Count];

            if (ShouldUseParallel(items.Count))
            {
                // Parallel bulk convert
                Parallel.For(0, items.Count, i =>
                {
                    result[i] = ConvertEntity(items[i], i);
                });
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                    result[i] = ConvertEntity(items[i], i);
            }

            return result;
        }

        /// <summary>
        /// Convert single entity -> InternalRow. Uses pooled CellValue[].
        /// </summary>
        public InternalRow ConvertEntity(T entity, int index = 0)
        {
            var buffer = _pool.Rent(_columnCount);

            for (int c = 0; c < _columnCount; c++)
            {
                object? raw = null;
                try { raw = _propertyGetters[c](entity); }
                catch { raw = null; }

                buffer[c] = ConvertToCellValue(raw);
            }
            var handle = new BufferHandle<CellValue>(buffer, _columnCount, _pool);
            return new InternalRow(
                index,
                GetIdFromEntity(entity),
                GetRowVersionFromEntity(entity),
                entity,
                handle
            );
        }

        /// <summary>
        /// Convert raw object into CellValue struct
        /// (CellValue is a small discriminated struct defined in Core).
        /// </summary>
        private static CellValue ConvertToCellValue(object? raw)
        {
            if (raw == null) return default;
            return raw switch
            {
                double d => CellValue.FromDouble(d),
                float f => CellValue.FromDouble(f),
                decimal m => CellValue.FromDouble((double)m),
                int i => CellValue.FromDouble(i),
                long l => CellValue.FromDouble(l),
                short s => CellValue.FromDouble(s),
                byte b => CellValue.FromDouble(b),
                bool bo => CellValue.FromBool(bo),
                string s => CellValue.FromString(s, StringPool.Shared),
                DateTime dt => CellValue.FromDateTime(dt),
                _ => CellValue.Empty
            };
        }


        // Helpers to extract Id/RowVersion via reflection for generic T (fast compiled delegates could be used)
        private static long GetIdFromEntity(T entity)
        {
            // prefer if T implements IEntity
            if (entity is IEntity ie) return ie.Id;
            // fallback: try property "Id" via reflection
            var pi = typeof(T).GetProperty("Id");
            if (pi != null && pi.PropertyType == typeof(long))
                return (long)(pi.GetValue(entity) ?? 0L);
            return 0L;
        }

        private static long GetRowVersionFromEntity(T entity)
        {
            if (entity is IEntity ie) return ie.RowVersion;
            var pi = typeof(T).GetProperty("RowVersion");
            if (pi != null && pi.PropertyType == typeof(long))
                return (long)(pi.GetValue(entity) ?? 0L);
            return 0L;
        }

        private bool ShouldUseParallel(int count) => count > 2000 && _columnCount > 5;


        public void Dispose()
        {
            if (_disposed) return;
            // nothing specific to dispose for ArrayPool
            _disposed = true;
        }
    }
}
