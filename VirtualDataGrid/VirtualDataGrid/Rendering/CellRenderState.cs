using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Rendering
{
    /// <summary>
    /// Per-cell render context passed to ICellRenderer.Render.
    /// Keep small and readonly.
    /// </summary>
    public readonly struct CellRenderState
    {
        public Rect CellRect { get; }
        public CellValue Value { get; }
        public bool IsSelected { get; }
        public bool IsHovered { get; }
        public int RowIndex { get; }
        public bool IsFocused { get; }
        public int ColumnIndex { get; }
        public ColumnSnapshot Column { get; }
        public double RowHeight { get; }

        public CellRenderState(Rect celRect, CellValue value, bool isSelected, bool isHovered, int rowIndex, int colIndex, bool isFocused, ColumnSnapshot column, double rowHeight)
        {
            CellRect = celRect;
            Value = value;
            IsSelected = isSelected;
            IsHovered = isHovered;
            RowIndex = rowIndex;
            ColumnIndex = colIndex;
            IsFocused = isFocused;
            Column = column;
            RowHeight = rowHeight;
        }
    }
}
