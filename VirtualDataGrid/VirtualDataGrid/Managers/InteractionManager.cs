using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace VirtualDataGrid.Managers
{
    /// <summary>
    /// InteractionManager
    /// -------------------
    /// - Menangani semua interaksi UI (mouse dan keyboard).
    /// - Menjadi penghubung antara input user ↔ logika internal grid.
    /// - Menginformasikan ke: ColumnManager, ScrollManager, SelectionManager, dan Renderer.
    /// 
    /// Contoh event:
    ///   - Klik kiri: pilih cell/row.
    ///   - Drag header: pindahkan kolom.
    ///   - Hover cell: highlight cell.
    ///   - Scroll wheel: ubah viewport.
    ///   - Keyboard: navigasi / shortcut.
    /// </summary>
    public class InteractionManager
    {
        private readonly Controls.VirtualDataGrid _grid;

        private int _hoveredRowIndex = -1;
        private bool _isDragging;
        private Point _dragStartPoint;
        private int _resizingColumnIndex = -1;
        private double _originalColumnWidth;

        public event EventHandler<RowHoverEventArgs> RowHoverChanged;
        public event EventHandler<RowClickEventArgs> RowClick;
        public event EventHandler<CellClickEventArgs> CellClick;
        public event EventHandler<ColumnResizeEventArgs> ColumnResizeStarted;
        public event EventHandler<ColumnResizeEventArgs> ColumnResizeChanged;
        public event EventHandler<ColumnResizeEventArgs> ColumnResizeCompleted;
        public event EventHandler<KeyboardNavEventArgs> KeyboardNavigation;

        public int HoveredRowIndex => _hoveredRowIndex;
        public bool IsColumnResizing => _resizingColumnIndex >= 0;

        public InteractionManager(Controls.VirtualDataGrid grid)
        {
            _grid = grid;
            SubscribeToInputEvents();
        }

        private void SubscribeToInputEvents()
        {
            _grid.MouseMove += OnMouseMove;
            _grid.MouseLeave += OnMouseLeave;
            _grid.MouseLeftButtonDown += OnMouseLeftButtonDown;
            _grid.MouseLeftButtonUp += OnMouseLeftButtonUp;
            _grid.MouseRightButtonDown += OnMouseRightButtonDown;
            _grid.MouseDoubleClick += OnMouseDoubleClick;
            _grid.KeyDown += OnKeyDown;
            _grid.KeyUp += OnKeyUp;
            _grid.LostMouseCapture += OnLostMouseCapture;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(_grid);
            var rowIndex = CalculateRowIndex(position.Y);

            // Update hover
            if (rowIndex != _hoveredRowIndex)
            {
                _hoveredRowIndex = rowIndex;
                RowHoverChanged?.Invoke(this, new RowHoverEventArgs(_hoveredRowIndex));
            }

            // Handle column resizing
            if (_isDragging && _resizingColumnIndex >= 0)
            {
                var delta = position.X - _dragStartPoint.X;
                var newWidth = Math.Max(_grid.Columns[_resizingColumnIndex].MinWidth,
                    _originalColumnWidth + delta);

                ColumnResizeChanged?.Invoke(this,
                    new ColumnResizeEventArgs(_resizingColumnIndex, newWidth));
            }
            else
            {
                // Check if mouse is in resize area
                var columnIndex = CalculateColumnIndex(position.X);
                if (IsInResizeArea(position.X, columnIndex))
                {
                    _grid.Cursor = Cursors.SizeWE;
                }
                else
                {
                    _grid.Cursor = Cursors.Arrow;
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(_grid);
            var rowIndex = CalculateRowIndex(position.Y);
            var columnIndex = CalculateColumnIndex(position.X);

            // Check for column resize
            if (IsInResizeArea(position.X, columnIndex))
            {
                StartColumnResize(columnIndex, position);
                e.Handled = true;
                return;
            }

            if (rowIndex >= 0 && rowIndex < _grid.TotalRowCount)
            {
                // Handle selection
                bool extend = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool toggle = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

                _grid.SelectionManager.SelectRow(rowIndex, extend, toggle);

                // Raise cell click event
                var cellValue = _grid.GetCellValue(rowIndex, columnIndex);
                CellClick?.Invoke(this, new CellClickEventArgs(rowIndex, columnIndex, cellValue));

                // Raise row click event
                var rowData = _grid.GetRowData(rowIndex);
                RowClick?.Invoke(this, new RowClickEventArgs(rowIndex, rowData));
            }

            e.Handled = true;
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(_grid);
            var rowIndex = CalculateRowIndex(position.Y);

            if (rowIndex >= 0 && rowIndex < _grid.TotalRowCount)
            {
                var rowData = _grid.GetRowData(rowIndex);
                RowClick?.Invoke(this, new RowClickEventArgs(rowIndex, rowData) { IsDoubleClick = true });
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var args = new KeyboardNavEventArgs(e.Key, Keyboard.Modifiers);
            KeyboardNavigation?.Invoke(this, args);

            if (args.Handled)
            {
                e.Handled = true;
                return;
            }

            // Default keyboard handling
            switch (e.Key)
            {
                case Key.Up:
                    _grid.SelectionManager.MoveSelection(-1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    _grid.SelectionManager.MoveSelection(1);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    _grid.SelectionManager.MoveSelection(-CalculatePageSize());
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    _grid.SelectionManager.MoveSelection(CalculatePageSize());
                    e.Handled = true;
                    break;
                case Key.Home:
                    _grid.SelectionManager.SelectFirst();
                    e.Handled = true;
                    break;
                case Key.End:
                    _grid.SelectionManager.SelectLast();
                    e.Handled = true;
                    break;
                case Key.A when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                    _grid.SelectionManager.SelectAll();
                    e.Handled = true;
                    break;
                case Key.Space:
                    _grid.SelectionManager.ToggleSelection();
                    e.Handled = true;
                    break;
            }
        }

        private void StartColumnResize(int columnIndex, Point startPoint)
        {
            _resizingColumnIndex = columnIndex;
            _originalColumnWidth = _grid.Columns[columnIndex].Width;
            _dragStartPoint = startPoint;
            _isDragging = true;
            _grid.CaptureMouse();

            ColumnResizeStarted?.Invoke(this,
                new ColumnResizeEventArgs(columnIndex, _originalColumnWidth));
        }

        private void CompleteColumnResize()
        {
            if (_resizingColumnIndex >= 0)
            {
                ColumnResizeCompleted?.Invoke(this,
                    new ColumnResizeEventArgs(_resizingColumnIndex,
                    _grid.Columns[_resizingColumnIndex].Width));

                _resizingColumnIndex = -1;
                _isDragging = false;
                _grid.ReleaseMouseCapture();
                _grid.Cursor = Cursors.Arrow;
            }
        }

        private bool IsInResizeArea(double x, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _grid.Columns.Count) return false;

            var columnStartX = CalculateColumnStartX(columnIndex);
            var columnEndX = columnStartX + _grid.Columns[columnIndex].Width;

            // Resize area is 5 pixels at the right edge of the column
            return x >= columnEndX - 5 && x <= columnEndX + 5;
        }

        private int CalculateRowIndex(double y)
        {
            var scrollOffset = _grid.ScrollViewer?.VerticalOffset ?? 0;
            var rowHeight = _grid.RowHeight;
            return (int)((y + scrollOffset) / rowHeight);
        }

        private int CalculateColumnIndex(double x)
        {
            double currentX = 0;
            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                currentX += _grid.Columns[i].Width;
                if (x <= currentX) return i;
            }
            return -1;
        }

        private double CalculateColumnStartX(int columnIndex)
        {
            double x = 0;
            for (int i = 0; i < columnIndex; i++)
            {
                x += _grid.Columns[i].Width;
            }
            return x;
        }

        private int CalculatePageSize()
        {
            var viewportHeight = _grid.ScrollViewer?.ViewportHeight ?? _grid.ActualHeight;
            return (int)(viewportHeight / _grid.RowHeight);
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                CompleteColumnResize();
                e.Handled = true;
            }
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                CompleteColumnResize();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredRowIndex != -1)
            {
                _hoveredRowIndex = -1;
                RowHoverChanged?.Invoke(this, new RowHoverEventArgs(-1));
            }
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Handle right-click for context menu
            var position = e.GetPosition(_grid);
            var rowIndex = CalculateRowIndex(position.Y);

            if (rowIndex >= 0)
            {
                _grid.SelectionManager.SelectRow(rowIndex);
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            // Handle key up events if needed
        }

        public void ScrollToRow(int rowIndex)
        {
            var rowHeight = _grid.RowHeight;
            var targetOffset = rowIndex * rowHeight;
            _grid.ScrollViewer?.ScrollToVerticalOffset(targetOffset);
        }
    }

    public class RowHoverEventArgs : EventArgs
    {
        public int RowIndex { get; }

        public RowHoverEventArgs(int rowIndex)
        {
            RowIndex = rowIndex;
        }
    }

    public class RowClickEventArgs : EventArgs
    {
        public int RowIndex { get; }
        public object RowData { get; }
        public bool IsDoubleClick { get; set; }

        public RowClickEventArgs(int rowIndex, object rowData)
        {
            RowIndex = rowIndex;
            RowData = rowData;
        }
    }

    public class CellClickEventArgs : EventArgs
    {
        public int RowIndex { get; }
        public int ColumnIndex { get; }
        public object CellValue { get; }

        public CellClickEventArgs(int rowIndex, int columnIndex, object cellValue)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            CellValue = cellValue;
        }
    }

    public class ColumnResizeEventArgs : EventArgs
    {
        public int ColumnIndex { get; }
        public double NewWidth { get; }

        public ColumnResizeEventArgs(int columnIndex, double newWidth)
        {
            ColumnIndex = columnIndex;
            NewWidth = newWidth;
        }
    }

    public class KeyboardNavEventArgs : EventArgs
    {
        public Key Key { get; }
        public ModifierKeys Modifiers { get; }
        public bool Handled { get; set; }

        public KeyboardNavEventArgs(Key key, ModifierKeys modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }
    }
}
