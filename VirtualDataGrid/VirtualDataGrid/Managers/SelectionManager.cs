using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Managers
{
    //(FULL SELECTION)
    public class SelectionManager
    {
        private readonly Controls.VirtualDataGrid _grid;
        private readonly HashSet<int> _selectedIndices;
        private int _anchorIndex = -1;
        private int _currentIndex = -1;

        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
        public event EventHandler<SelectionPreviewEventArgs> SelectionPreview;

        public SelectionManager(Controls.VirtualDataGrid grid)
        {
            _grid = grid;
            _selectedIndices = new HashSet<int>();
        }

        public void SelectRow(int rowIndex, bool extendSelection = false, bool toggleSelection = false)
        {
            if (rowIndex < 0 || rowIndex >= _grid.TotalRowCount) return;

            var oldSelection = new HashSet<int>(_selectedIndices);
            var previewArgs = new SelectionPreviewEventArgs(oldSelection, rowIndex, extendSelection, toggleSelection);
            SelectionPreview?.Invoke(this, previewArgs);

            if (previewArgs.Cancel) return;

            switch (_grid.SelectionMode)
            {
                case SelectionMode.Single:
                    SelectSingle(rowIndex);
                    break;
                case SelectionMode.Multiple:
                    SelectMultiple(rowIndex, toggleSelection);
                    break;
                case SelectionMode.Extended:
                    SelectExtended(rowIndex, extendSelection, toggleSelection);
                    break;
            }

            _currentIndex = rowIndex;
            RaiseSelectionChanged(oldSelection);
        }

        public void SelectRange(int fromIndex, int toIndex)
        {
            var oldSelection = new HashSet<int>(_selectedIndices);
            _selectedIndices.Clear();

            var start = Math.Max(0, Math.Min(fromIndex, toIndex));
            var end = Math.Min(_grid.TotalRowCount - 1, Math.Max(fromIndex, toIndex));

            for (int i = start; i <= end; i++)
            {
                _selectedIndices.Add(i);
            }

            RaiseSelectionChanged(oldSelection);
        }

        public void ClearSelection()
        {
            var oldSelection = new HashSet<int>(_selectedIndices);
            _selectedIndices.Clear();
            _anchorIndex = -1;
            _currentIndex = -1;
            RaiseSelectionChanged(oldSelection);
        }

        public void SelectAll()
        {
            if (_grid.SelectionMode == SelectionMode.Single) return;

            var oldSelection = new HashSet<int>(_selectedIndices);
            _selectedIndices.Clear();

            for (int i = 0; i < _grid.TotalRowCount; i++)
            {
                _selectedIndices.Add(i);
            }

            RaiseSelectionChanged(oldSelection);
        }
        public void SelectFirst()
        {
            if (_grid.TotalRowCount > 0)
                SelectRow(0);
        }

        public void SelectLast()
        {
            if (_grid.TotalRowCount > 0)
                SelectRow(_grid.TotalRowCount - 1);
        }

        public void ToggleSelection()
        {
            if (_currentIndex >= 0)
                ToggleSelection(_currentIndex);
        }

        public void MoveSelection(int delta)
        {
            if (_grid.TotalRowCount == 0) return;

            var newIndex = Math.Max(0, Math.Min(_grid.TotalRowCount - 1, _currentIndex + delta));
            SelectRow(newIndex);
        }

        public IEnumerable<int> GetSelectedIndices() => _selectedIndices;
        public bool IsSelected(int rowIndex) => _selectedIndices.Contains(rowIndex);
        public int SelectedCount => _selectedIndices.Count;
        public int CurrentIndex => _currentIndex;
        public int AnchorIndex => _anchorIndex;

        private void SelectSingle(int rowIndex)
        {
            _selectedIndices.Clear();
            _selectedIndices.Add(rowIndex);
            _anchorIndex = rowIndex;
        }

        private void SelectMultiple(int rowIndex, bool toggle)
        {
            if (toggle)
            {
                if (_selectedIndices.Contains(rowIndex))
                    _selectedIndices.Remove(rowIndex);
                else
                    _selectedIndices.Add(rowIndex);
            }
            else
            {
                _selectedIndices.Clear();
                _selectedIndices.Add(rowIndex);
            }
            _anchorIndex = rowIndex;
        }

        private void SelectExtended(int rowIndex, bool extend, bool toggle)
        {
            if (!extend && !toggle)
            {
                SelectSingle(rowIndex);
            }
            else if (toggle)
            {
                ToggleSelection(rowIndex);
            }
            else if (extend)
            {
                SelectRange(_anchorIndex, rowIndex);
            }
        }

        public void ToggleSelection(int rowIndex)
        {
            if (_selectedIndices.Contains(rowIndex))
                _selectedIndices.Remove(rowIndex);
            else
                _selectedIndices.Add(rowIndex);
        }

        private void RaiseSelectionChanged(HashSet<int> oldSelection)
        {
            var added = _selectedIndices.Except(oldSelection).ToList();
            var removed = oldSelection.Except(_selectedIndices).ToList();

            if (added.Any() || removed.Any())
            {
                SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(removed, added));
            }
        }
    }

    public class SelectionChangedEventArgs : EventArgs
    {
        public IList<int> RemovedIndices { get; }
        public IList<int> AddedIndices { get; }

        public SelectionChangedEventArgs(IEnumerable<int> removed, IEnumerable<int> added)
        {
            RemovedIndices = removed.ToList();
            AddedIndices = added.ToList();
        }
    }

    public class SelectionPreviewEventArgs : EventArgs
    {
        public HashSet<int> CurrentSelection { get; }
        public int TargetRowIndex { get; }
        public bool ExtendSelection { get; }
        public bool ToggleSelection { get; }
        public bool Cancel { get; set; }

        public SelectionPreviewEventArgs(HashSet<int> currentSelection, int targetRowIndex,
            bool extendSelection, bool toggleSelection)
        {
            CurrentSelection = currentSelection;
            TargetRowIndex = targetRowIndex;
            ExtendSelection = extendSelection;
            ToggleSelection = toggleSelection;
        }
    }
}