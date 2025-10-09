using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Data
{
    /// <summary>
    /// Filter & Sort engine operating on InternalRow slices.
    /// - Filters are compiled delegates that operate on the original item or on InternalRow values.
    /// - Sort uses comparer that examines CellValue types (numeric, stringId, bool).
    /// - Designed to run off UI thread.
    /// </summary>
    public sealed class FilterSortEngine
    {
        // Per-column predicate keyed by column index
        private readonly Dictionary<int, Func<InternalRow, bool>> _columnFilters = new();

        // Global filter predicate (e.g. search)
        private Func<InternalRow, bool>? _globalFilter;

        // Sorting: list of (columnIndex, ascending)
        private readonly List<(int ColumnIndex, bool Ascending)> _sorts = new();

        public FilterSortEngine() { }

        #region Filter API
        /// <summary>
        /// Terapkan global filter. Biasanya dipakai untuk search text.
        /// </summary>
        public void ApplyGlobalFilter(Func<InternalRow, bool>? predicate)
        {
            _globalFilter = predicate;
        }

        /// <summary>
        /// Global filter text search: semua kolom dicek.
        /// </summary>
        public void SetGlobalTextFilter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _globalFilter = null;
                return;
            }

            var needle = text.Trim();
            _globalFilter = row =>
            {
                foreach (var cell in row.Cells.Span)
                {
                    if (cell.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            };
        }

        /// <summary>
        /// Set filter per kolom.
        /// </summary>
        public void SetColumnFilter(int columnIndex, Func<InternalRow, bool> predicate)
        {
            if (predicate == null) _columnFilters.Remove(columnIndex);
            else _columnFilters[columnIndex] = predicate;
        }

        public void ClearColumnFilter(int columnIndex) => _columnFilters.Remove(columnIndex);
        public void ClearAllFilters()
        {
            _columnFilters.Clear();
            _globalFilter = null;
        }

        #endregion

        #region Sort API
        public void SetSorts(IEnumerable<(int ColumnIndex, bool Ascending)> sorts)
        {
            _sorts.Clear();
            _sorts.AddRange(sorts);
        }

        public void ClearSorts() => _sorts.Clear();
        #endregion

        #region Core Apply
        /// <summary>
        /// Apply filters and sorts on a ReadOnlySpan of rows.
        /// Returns new array (filtered and sorted).
        /// </summary>
        public InternalRow[] Apply(ReadOnlySpan<InternalRow> rows)
        {
            // FILTER
            InternalRow[] filtered;
            if (_globalFilter == null && _columnFilters.Count == 0)
            {
                // cheap copy
                filtered = rows.ToArray();
            }
            else
            {
                var list = new List<InternalRow>(rows.Length);
                foreach (var r in rows)
                {
                    if (_globalFilter != null && !_globalFilter(r)) continue;

                    bool ok = true;
                    if (_columnFilters.Count > 0)
                    {
                        foreach (var kv in _columnFilters)
                        {
                            if (!kv.Value(r)) { ok = false; break; }
                        }
                    }
                    if (ok) list.Add(r);
                }
                filtered = list.ToArray();
            }

            // SORT
            if (_sorts.Count == 0 || filtered.Length <= 1)
                return filtered;

            Array.Sort(filtered, CompareRows);
            return filtered;
        }

        private int CompareRows(InternalRow a, InternalRow b)
        {
            foreach (var (col, asc) in _sorts)
            {
                var va = a.GetValue(col);
                var vb = b.GetValue(col);

                int cmp = CompareCell(va, vb);
                if (cmp != 0) return asc ? cmp : -cmp;
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareCell(CellValue a, CellValue b)
        {
            if (a.IsNumeric && b.IsNumeric)
                return a.NumericValue.CompareTo(b.NumericValue);

            if (a.IsString && b.IsString)
                return a.StringId.CompareTo(b.StringId);

            if (a.IsBool && b.IsBool)
                return a.BoolValue.CompareTo(b.BoolValue);

            if (a.IsDate && b.IsDate)
                return a.DateValue.CompareTo(b.DateValue);

            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }
        #endregion
    }
}
