using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using VirtualDataGrid.Controls;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// Collection of VirtualDataGridColumn - non-generic untuk flexibility
    /// </summary>
    public class ColumnCollection : ObservableCollection<VirtualDataGridColumn>
    {
        private readonly Dictionary<string, VirtualDataGridColumn> _columnMap;
        private bool _isReordering;

        public ColumnCollection()
        {
            _columnMap = new Dictionary<string, VirtualDataGridColumn>(StringComparer.OrdinalIgnoreCase);
            CollectionChanged += OnCollectionChanged;
        }

        #region Public Methods

        /// <summary>
        /// Get column by binding path
        /// </summary>
        public VirtualDataGridColumn this[string bindingPath]
        {
            get
            {
                if (string.IsNullOrEmpty(bindingPath)) return null;
                _columnMap.TryGetValue(bindingPath, out var column);
                return column;
            }
        }

        /// <summary>
        /// Check if binding path exists
        /// </summary>
        public bool ContainsBindingPath(string bindingPath) =>
            !string.IsNullOrEmpty(bindingPath) && _columnMap.ContainsKey(bindingPath);

        /// <summary>
        /// Try get column by binding path
        /// </summary>
        public bool TryGetColumn(string bindingPath, out VirtualDataGridColumn column)
        {
            column = null;
            if (string.IsNullOrEmpty(bindingPath)) return false;
            return _columnMap.TryGetValue(bindingPath, out column);
        }

        /// <summary>
        /// Total width of all visible columns
        /// </summary>
        public double TotalWidth => GetVisibleColumns().Sum(column => column.Width);

        /// <summary>
        /// Total width of frozen columns
        /// </summary>
        public double FrozenWidth => GetVisibleColumns().Where(c => c.IsFrozen).Sum(column => column.Width);

        /// <summary>
        /// Count of frozen columns
        /// </summary>
        public int FrozenColumnCount => this.Count(column => column.IsFrozen);

        /// <summary>
        /// Get visible columns in display order
        /// </summary>
        public IEnumerable<VirtualDataGridColumn> GetVisibleColumns() =>
            this.Where(column => column.IsVisible)
                .OrderBy(c => c.DisplayIndex >= 0 ? c.DisplayIndex : IndexOf(c));

        /// <summary>
        /// Add new column with fluent API
        /// </summary>
        public VirtualDataGridColumn AddColumn(string header, string bindingPath, double width = 100,
            bool isFrozen = false, string formatString = null, ColumnType columnType = ColumnType.Text)
        {
            var column = new VirtualDataGridColumn
            {
                Header = header,
                BindingPath = bindingPath,
                Width = width,
                IsFrozen = isFrozen,
                FormatString = formatString,
                ColumnType = columnType
            };

            Add(column);
            return column;
        }

        /// <summary>
        /// Add multiple columns
        /// </summary>
        public void AddRange(IEnumerable<VirtualDataGridColumn> columns)
        {
            if (columns == null) return;

            foreach (var column in columns)
            {
                Add(column);
            }
        }

        /// <summary>
        /// Remove column by binding path
        /// </summary>
        public bool RemoveColumn(string bindingPath)
        {
            if (TryGetColumn(bindingPath, out var column))
            {
                return Remove(column);
            }
            return false;
        }

        /// <summary>
        /// Auto-size all columns based on sample data
        /// </summary>
        public void AutoSizeAllColumns(IEnumerable<object> sampleItems = null, int sampleSize = 30, double fontSize = 12.0)
        {
            foreach (var column in this)
            {
                column.AutoSize(sampleItems, sampleSize, null, fontSize);
            }
        }

        /// <summary>
        /// Move column and maintain display order
        /// </summary>
        public void MoveColumn(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;
            if (oldIndex < 0 || oldIndex >= Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if (newIndex < 0 || newIndex >= Count) throw new ArgumentOutOfRangeException(nameof(newIndex));

            _isReordering = true;
            try
            {
                var column = this[oldIndex];
                Move(oldIndex, newIndex);
                RecalculateDisplayIndexes();
                ColumnsReordered?.Invoke(this, new ColumnsReorderedEventArgs(oldIndex, newIndex));
            }
            finally
            {
                _isReordering = false;
            }
        }

        /// <summary>
        /// Reset to default columns based on type (optional)
        /// </summary>
        public void ResetToDefault(Type entityType = null)
        {
            Clear();

            if (entityType != null)
            {
                AddDefaultColumns(entityType);
            }
        }

        /// <summary>
        /// Get core configs for data processing
        /// </summary>
        public ColumnConfig[] GetCoreConfigs()
        {
            //return this.Select(column => new ColumnConfig(
            //    column.BindingPath,
            //    column.ColumnType,
            //    column.FormatString
            //)).ToArray();
            return null;
        }

        /// <summary>
        /// Get snapshots for rendering
        /// </summary>
        //public ColumnSnapshot[] GetSnapshots()
        //{
        //    return this.Select(column => new ColumnSnapshot(
        //        column.Header,
        //        column.Width,
        //        column.IsFrozen,
        //        column.TextAlignment,
        //        column.ColumnType
        //    )).ToArray();
        //}

        #endregion

        #region Private Methods

        private void AddDefaultColumns(Type entityType)
        {
            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties.Where(p => IsSupportedType(p.PropertyType)))
            {
                var column = new VirtualDataGridColumn
                {
                    Header = prop.Name,
                    BindingPath = prop.Name,
                    Width = GetDefaultWidth(prop.PropertyType),
                    ColumnType = GetColumnType(prop.PropertyType)
                };

                Add(column);
            }
        }

        private static double GetDefaultWidth(Type propertyType)
        {
            return propertyType switch
            {
                Type t when t == typeof(string) => 120,
                Type t when t == typeof(int) || t == typeof(long) => 80,
                Type t when t == typeof(decimal) || t == typeof(double) => 100,
                Type t when t == typeof(DateTime) => 150,
                Type t when t == typeof(bool) => 60,
                _ => 100
            };
        }

        private static ColumnType GetColumnType(Type propertyType)
        {
            return propertyType switch
            {
                Type t when t == typeof(string) => ColumnType.Text,
                Type t when t == typeof(int) || t == typeof(long) ||
                           t == typeof(decimal) || t == typeof(double) || t == typeof(float) => ColumnType.Number,
                Type t when t == typeof(DateTime) => ColumnType.Date,
                Type t when t == typeof(bool) => ColumnType.CheckBox,
                _ => ColumnType.Text
            };
        }

        private static bool IsSupportedType(Type type)
        {
            return type == typeof(string) || type == typeof(int) || type == typeof(long) ||
                   type == typeof(decimal) || type == typeof(double) || type == typeof(float) ||
                   type == typeof(DateTime) || type == typeof(bool) || type.IsEnum;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateColumnMap(e);
            RecalculateDisplayIndexes();

            if (!_isReordering)
            {
                ColumnsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateColumnMap(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (VirtualDataGridColumn column in e.NewItems)
                    {
                        if (!string.IsNullOrEmpty(column.BindingPath))
                        {
                            _columnMap[column.BindingPath] = column;
                        }
                        column.PropertyChanged += OnColumnPropertyChanged;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (VirtualDataGridColumn column in e.OldItems)
                    {
                        if (!string.IsNullOrEmpty(column.BindingPath))
                        {
                            _columnMap.Remove(column.BindingPath);
                        }
                        column.PropertyChanged -= OnColumnPropertyChanged;
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _columnMap.Clear();
                    foreach (var column in this)
                    {
                        if (!string.IsNullOrEmpty(column.BindingPath))
                        {
                            _columnMap[column.BindingPath] = column;
                        }
                        column.PropertyChanged += OnColumnPropertyChanged;
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    _columnMap.Clear();
                    foreach (var column in this)
                    {
                        if (!string.IsNullOrEmpty(column.BindingPath))
                        {
                            _columnMap[column.BindingPath] = column;
                        }
                    }
                    break;
            }
        }

        private void OnColumnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var column = (VirtualDataGridColumn)sender;

            // Update mapping if BindingPath changed
            if (e.PropertyName == nameof(VirtualDataGridColumn.BindingPath))
            {
                _columnMap.Clear();
                foreach (var col in this)
                {
                    if (!string.IsNullOrEmpty(col.BindingPath))
                    {
                        _columnMap[col.BindingPath] = col;
                    }
                }
            }

            // Enforce frozen columns at front
            if (e.PropertyName == nameof(VirtualDataGridColumn.IsFrozen) && column.IsFrozen)
            {
                EnforceFrozenColumnsAtFront();
            }

            ColumnPropertyChanged?.Invoke(this, new ColumnPropertyChangedEventArgs(column, e.PropertyName));
        }

        private void RecalculateDisplayIndexes()
        {
            for (int i = 0; i < Count; i++)
            {
                this[i].DisplayIndex = i;
            }
        }

        private void EnforceFrozenColumnsAtFront()
        {
            var frozenColumns = this.Where(c => c.IsFrozen).ToList();
            var nonFrozenColumns = this.Where(c => !c.IsFrozen).ToList();

            var newOrder = frozenColumns.Concat(nonFrozenColumns).ToList();

            for (int i = 0; i < newOrder.Count; i++)
            {
                var currentColumn = this[i];
                var desiredColumn = newOrder[i];

                if (!ReferenceEquals(currentColumn, desiredColumn))
                {
                    var currentIndex = IndexOf(desiredColumn);
                    if (currentIndex > i)
                    {
                        Move(currentIndex, i);
                    }
                }
            }
        }

        #endregion

        #region Events

        public event EventHandler<ColumnPropertyChangedEventArgs> ColumnPropertyChanged;
        public event EventHandler ColumnsChanged;
        public event EventHandler<ColumnsReorderedEventArgs> ColumnsReordered;

        #endregion
    }

    // <summary>
    /// EventArgs untuk column property changes
    /// </summary>
    public class ColumnPropertyChangedEventArgs : EventArgs
    {
        public VirtualDataGridColumn Column { get; }
        public string PropertyName { get; }

        public ColumnPropertyChangedEventArgs(VirtualDataGridColumn column, string propertyName)
        {
            Column = column;
            PropertyName = propertyName;
        }
    }

    /// <summary>
    /// EventArgs untuk column reordering
    /// </summary>
    public class ColumnsReorderedEventArgs : EventArgs
    {
        public int OldIndex { get; }
        public int NewIndex { get; }

        public ColumnsReorderedEventArgs(int oldIndex, int newIndex)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }
    }
}