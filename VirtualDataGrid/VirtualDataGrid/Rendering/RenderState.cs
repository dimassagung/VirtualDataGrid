using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using VirtualDataGrid.Controls;
using VirtualDataGrid.Core;


namespace VirtualDataGrid.Rendering
{
    /// <summary>
    /// Shared render state between managers and renderer.
    /// Thread-safe: lock digunakan untuk update background (summary, async ops).
    /// 
    /// Konsep: satu container state untuk seluruh kondisi visual grid
    /// (viewport, data visible, selection, summary, theme).
    /// Renderer hanya baca state ini (read-only access).
    /// </summary>
    public sealed class RenderState
    {
        private readonly object _lock = new();

        // === DATA ===
        public ReadOnlyMemory<InternalRow> _visibleRows { get; private set; } = ReadOnlyMemory<InternalRow>.Empty;
        private ReadOnlyMemory<CellValue> _visibleCells = ReadOnlyMemory<CellValue>.Empty;
        private ColumnSnapshot[] _columns = Array.Empty<ColumnSnapshot>();
        private HashSet<int> _selectedIndices = new();
        private object[] _summaryValues = Array.Empty<object>();

        // === VIEWPORT ===
        private int _topRowIndex = 0;
        private int _visibleRowCount = 0;
        private double _horizontalOffset = 0.0;
        private double _verticalOffset = 0.0;
        private double _viewWidth = 0.0;
        private double _viewHeight = 0.0;
        private double _rowHeight = 32.0;
        private double _frozenColumnCount = 0;

        // === INTERACTION ===
        private int _focusedRowIndex = -1;
        private int _focusedColumnIndex = -1;
        private int _hoveredRowIndex = -1;

        // === THEME ===
        private string _theme = "Light";

        // === INVALIDATION FLAGS ===
        private bool _viewportInvalidated = true;
        private bool _columnsInvalidated = true;
        private bool _selectionInvalidated = false;
        private bool _summaryInvalidated = false;
        private bool _themeInvalidated = false;

        // === DEBUG ===
        private int _frameId = 0;
        private long _timestamp = Environment.TickCount64;

        #region METHODS UNTUK UPDATE STATE (Background Thread)

        /// <summary>
        /// Update data yang kelihatan di viewport
        /// Dipanggil waktu scroll atau data berubah
        /// </summary>
        public void UpdateVisibleRows(ReadOnlyMemory<InternalRow> visibleRows)
        {
            lock (_lock)
            {
                _visibleRows = visibleRows;
                _viewportInvalidated = true;
                _frameId++;
                _timestamp = Environment.TickCount64;
            }
        }

        public void UpdateVisibleCells(ReadOnlyMemory<CellValue> cells)
        {
            lock (_lock)
            {
                _visibleCells = cells;
                _viewportInvalidated = true;
            }
        }

        /// <summary>
        /// Update informasi viewport (scroll position, size, dll)
        /// </summary>
        public void UpdateViewport(int topIndex, int visibleCount, double hOffset, double vOffset,
                                 double viewWidth, double viewHeight, double rowHeight)
        {
            lock (_lock)
            {
                _topRowIndex = topIndex;
                _visibleRowCount = visibleCount;
                _horizontalOffset = hOffset;
                _verticalOffset = vOffset;
                _viewWidth = viewWidth;
                _viewHeight = viewHeight;
                _rowHeight = rowHeight;
                _viewportInvalidated = true;
                _frameId++;
                _timestamp = Environment.TickCount64;
            }
        }

        /// <summary>
        /// Update kolom (lebar, frozen, dll)
        /// </summary>

        public void UpdateColumns(ColumnSnapshot[] columns, int frozenColumns)
        {
            lock (_lock)
            {
                _columns = columns?.ToArray() ?? Array.Empty<ColumnSnapshot>();

                _frozenColumnCount = frozenColumns;
                _summaryValues = new object[_columns.Length]; // Reset summary
                _columnsInvalidated = true;
                _viewportInvalidated = true;
                _summaryInvalidated = true;
                _frameId++;
            }
        }

        /// <summary>
        /// Update selection (row yang dipilih)
        /// </summary>
        public void UpdateSelection(HashSet<int> selected, int hoveredIndex = -1, int focusedRowIndex = -1, int focusedColumnIndex = -1)
        {
            lock (_lock)
            {
                _selectedIndices = selected ?? new HashSet<int>();
                _hoveredRowIndex = hoveredIndex;
                _focusedRowIndex = focusedRowIndex;
                _focusedColumnIndex = focusedColumnIndex;
                _selectionInvalidated = true;
            }
        }

        public void UpdateFocus(int rowIndex, int columnIndex)
        {
            lock (_lock)
            {
                _focusedRowIndex = rowIndex;
                _focusedColumnIndex = columnIndex;
                _selectionInvalidated = true;
            }
        }

        public void UpdateHover(int rowIndex)
        {
            lock (_lock)
            {
                _hoveredRowIndex = rowIndex;
                _selectionInvalidated = true;
            }
        }

        public void UpdateSummaries(object[] summaries)
        {
            if (summaries == null) throw new ArgumentNullException(nameof(summaries));

            lock (_lock)
            {
                if (summaries.Length != _columns.Length)
                {
                    var newArr = new object[_columns.Length];
                    Array.Copy(summaries, newArr, Math.Min(summaries.Length, newArr.Length));
                    _summaryValues = newArr;
                }
                else
                {
                    _summaryValues = summaries;
                }
                _summaryInvalidated = true;
                _frameId++;
            }
        }

        public void UpdateTheme(string theme)
        {
            lock (_lock)
            {
                if (_theme != theme)
                {
                    _theme = theme;
                    _themeInvalidated = true;
                    _frameId++;
                }
            }
        }


        /// <summary>
        /// Manual trigger untuk render ulang
        /// </summary>
        public void InvalidateAll()
        {
            lock (_lock)
            {
                _viewportInvalidated = true;
                _columnsInvalidated = true;
                _selectionInvalidated = true;
                _summaryInvalidated = true;
                _themeInvalidated = true;
                _frameId++;
            }
        }

        #endregion

        #region METHODS UNTUK RENDERER (UI Thread)

        /// <summary>
        /// BUAT SNAPSHOT - Ambil "foto" state saat ini untuk rendering
        /// UI thread panggil ini, dapat data yang CONSISTENT dan IMMUTABLE
        /// </summary>

        public RenderSnapshot CreateSnapshot()
        {
            lock (_lock)
            {
                var snapshot = new RenderSnapshot(
                     _visibleRows,
                    _columns,
                     _topRowIndex,
                     _visibleRowCount,
                    _horizontalOffset,
                     _verticalOffset,
                    _viewWidth,
                    _viewHeight,
                    _rowHeight,
                     CalculateFrozenCount(),
                     new HashSet<int>(_selectedIndices),
                   _hoveredRowIndex,
                     _focusedRowIndex,
                     _focusedColumnIndex,
                     _summaryValues,
                     _theme,
                     _frameId,
                    _timestamp
                );

                _viewportInvalidated = false;
                _columnsInvalidated = false;
                _selectionInvalidated = false;
                _summaryInvalidated = false;
                _themeInvalidated = false;

                return snapshot;
            }
        }

        /// <summary>
        /// Hitung berapa kolom yang frozen (ada di depan)
        /// </summary>
        private int CalculateFrozenCount()
        {
            int count = 0;
            foreach (var col in _columns)
            {
                if (col.IsFrozen) count++;
                else break; // Frozen columns harus urut di depan
            }
            return count;
        }

        #endregion
    }

    /// <summary>
    /// SNAPSHOT - "Foto" state grid yang IMMUTABLE (tidak bisa diubah)
    /// Renderer baca ini tanpa perlu lock, dijamin CONSISTENT
    /// </summary>
    public readonly record struct RenderSnapshot(
        ReadOnlyMemory<InternalRow> VisibleRows,
        ColumnSnapshot[] Columns,
        int TopRowIndex,
        int VisibleRowCount,
        double HorizontalOffset,
        double VerticalOffset,
        double ViewWidth,
        double ViewHeight,
        double RowHeight,
        int FrozenColumnCount,
        HashSet<int> SelectedIndices,
        int HoveredRowIndex,
        int FocusedRowIndex,
        int FocusedColumnIndex,
        object[] SummaryValues,
        string Theme,
        int FrameId,
        long Timestamp
    )
    {
        // === 🎯 HELPER PROPERTIES UNTUK RENDERER ===

        /// <summary>Ada data yang bisa di-render?</summary>
        public bool HasData => !VisibleRows.IsEmpty;

        /// <summary>State valid untuk rendering?</summary>
        public bool IsValid => ViewWidth > 0 && ViewHeight > 0 && Columns?.Length > 0;

        /// <summary>Ada row yang selected?</summary>
        public bool HasSelection => SelectedIndices.Count > 0;

        // === 🎯 HELPER METHODS ===

        /// <summary>Cek apakah row ini selected</summary>
        public bool IsRowSelected(int rowIndex) => SelectedIndices.Contains(rowIndex);

        /// <summary>Total lebar semua kolom</summary>
        public double TotalColumnWidth => Columns?.Sum(col => col.Width) ?? 0;

        /// <summary>Total lebar kolom frozen</summary>
        public double FrozenWidth
        {
            get
            {
                if (Columns == null) return 0;
                double total = 0;
                for (int i = 0; i < FrozenColumnCount && i < Columns.Length; i++)
                    total += Columns[i].Width;
                return total;
            }
        }

        /// <summary>Dapatkan kolom berdasarkan index</summary>
        public ColumnSnapshot GetColumn(int index) =>
            Columns != null && index >= 0 && index < Columns.Length
            ? Columns[index]
            : default;

        /// <summary>Dapatkan visible column range berdasarkan horizontal scroll</summary>
        public (int start, int end) GetVisibleColumnRange()
        {
            if (Columns == null || Columns.Length == 0)
                return (0, 0);

            double accumulatedWidth = 0;
            int start = 0;
            int end = Columns.Length - 1;

            // Cari kolom mulai yang kelihatan
            for (int i = 0; i < Columns.Length; i++)
            {
                accumulatedWidth += Columns[i].Width;
                if (accumulatedWidth > HorizontalOffset)
                {
                    start = i;
                    break;
                }
            }

            // Cari kolom akhir yang kelihatan
            accumulatedWidth = 0;
            double visibleWidth = HorizontalOffset + ViewWidth;
            for (int i = start; i < Columns.Length; i++)
            {
                accumulatedWidth += Columns[i].Width;
                if (accumulatedWidth >= visibleWidth)
                {
                    end = i;
                    break;
                }
            }

            return (start, end);
        }
    }

    /// <summary>
    /// SNAPSHOT PER KOLOM - Data kolom yang diperlukan untuk rendering
    /// </summary>
    public readonly record struct ColumnSnapshot(string Header, double Width, bool IsFrozen);
}
