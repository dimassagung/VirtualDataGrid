using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// Selection behavior untuk grid.
    /// </summary>
    public enum SelectionMode
    {
        Single,     // ✅ hanya boleh pilih 1 row
        Multiple,   // ✅ bisa pilih banyak row (checkbox style)
        Extended    // ✅ bisa pakai Shift/Ctrl (kayak DataGrid asli)
    }

    /// <summary>
    /// Deskripsi untuk sorting kolom (digunakan FilterSortEngine).
    /// </summary>
    public sealed class SortDescription
    {
        /// <summary>
        /// Index kolom (berdasarkan urutan tampil).
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// Ascending / Descending.
        /// </summary>
        public ListSortDirection Direction { get; set; }

        /// <summary>
        /// Opsional: nama properti (untuk debugging atau binding UI).
        /// </summary>
        public string? PropertyName { get; set; }

        public SortDescription(int columnIndex, ListSortDirection direction, string propertyName = null)
        {
            ColumnIndex = columnIndex;
            Direction = direction;
            PropertyName = propertyName;
        }

        public override string ToString() =>
            $"{PropertyName ?? ColumnIndex.ToString()} ({Direction})";
    }

    /// <summary>
    /// Info posisi scroll (viewport state).
    /// </summary>
    public sealed class ScrollInfo
    {
        public double HorizontalOffset { get; }
        public double VerticalOffset { get; }

        public ScrollInfo(double horizontalOffset, double verticalOffset)
        {
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
        }

        public override string ToString() =>
            $"Scroll(H={HorizontalOffset}, V={VerticalOffset})";
    }


    public enum SummaryType
    {
        None,
        Sum,
        Average,
        Count,
        Min,
        Max
    }
}