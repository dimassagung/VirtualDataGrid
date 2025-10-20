using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VirtualDataGrid.Controls;

namespace VirtualDataGrid.Controls
{/// <summary>
 /// EventArgs untuk perubahan property pada kolom
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
    /// EventArgs untuk reordering kolom
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

    /// <summary>
    /// Koleksi kolom untuk VirtualDataGrid - Production ready dengan fitur lengkap
    /// </summary>
    public class ColumnCollection : ObservableCollection<VirtualDataGridColumn>
    {
        private readonly Dictionary<string, VirtualDataGridColumn> _columnMap;
        private bool _isReordering;

        /// <summary>
        /// Constructor default
        /// </summary>
        public ColumnCollection()
        {
            _columnMap = new Dictionary<string, VirtualDataGridColumn>(StringComparer.OrdinalIgnoreCase);
            CollectionChanged += OnCollectionChanged;
        }

        #region Properties yang Dihitung

        /// <summary>
        /// Indexer untuk akses kolom berdasarkan binding path
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
        /// Total lebar semua kolom (visible + hidden)
        /// </summary>
        public double TotalWidth => this.Sum(column => column.Width);

        /// <summary>
        /// Total lebar kolom yang visible
        /// </summary>
        public double TotalVisibleWidth => this.Where(column => column.IsVisible).Sum(column => column.Width);

        /// <summary>
        /// Total lebar kolom yang frozen
        /// </summary>
        public double FrozenWidth => this.Where(column => column.IsFrozen && column.IsVisible).Sum(column => column.Width);

        /// <summary>
        /// Jumlah kolom yang frozen
        /// </summary>
        public int FrozenColumnCount => this.Count(column => column.IsFrozen && column.IsVisible);

        /// <summary>
        /// Jumlah kolom yang visible
        /// </summary>
        public int VisibleColumnCount => this.Count(column => column.IsVisible);

        /// <summary>
        /// Apakah collection memiliki kolom yang frozen
        /// </summary>
        public bool HasFrozenColumns => this.Any(column => column.IsFrozen && column.IsVisible);

        #endregion

        #region Methods Publik

        /// <summary>
        /// Mengecek apakah collection mengandung kolom dengan binding path tertentu
        /// </summary>
        public bool ContainsBindingPath(string bindingPath) =>
            !string.IsNullOrEmpty(bindingPath) && _columnMap.ContainsKey(bindingPath);

        /// <summary>
        /// Mencoba mendapatkan kolom berdasarkan binding path
        /// </summary>
        public bool TryGetColumn(string bindingPath, out VirtualDataGridColumn column)
        {
            column = null;
            if (string.IsNullOrEmpty(bindingPath)) return false;
            return _columnMap.TryGetValue(bindingPath, out column);
        }

        /// <summary>
        /// Mendapatkan semua kolom yang visible dalam urutan display index
        /// </summary>
        public IEnumerable<VirtualDataGridColumn> GetVisibleColumns() =>
            this.Where(column => column.IsVisible)
                .OrderBy(c => c.DisplayIndex >= 0 ? c.DisplayIndex : IndexOf(c));

        /// <summary>
        /// Mendapatkan semua kolom yang frozen
        /// </summary>
        public IEnumerable<VirtualDataGridColumn> GetFrozenColumns() =>
            this.Where(column => column.IsFrozen && column.IsVisible);

        /// <summary>
        /// Mendapatkan semua kolom yang tidak frozen
        /// </summary>
        public IEnumerable<VirtualDataGridColumn> GetNonFrozenColumns() =>
            this.Where(column => !column.IsFrozen && column.IsVisible);

        /// <summary>
        /// Mencari kolom berdasarkan binding path
        /// </summary>
        public VirtualDataGridColumn GetByBinding(string bindingPath)
        {
            return this.FirstOrDefault(c => string.Equals(c.BindingPath, bindingPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Mencari kolom berdasarkan header text
        /// </summary>
        public VirtualDataGridColumn GetByHeader(string header)
        {
            return this.FirstOrDefault(c => string.Equals(c.Header, header, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Mencari kolom berdasarkan tag
        /// </summary>
        public VirtualDataGridColumn GetByTag(object tag)
        {
            return this.FirstOrDefault(c => object.Equals(c.Tag, tag));
        }

        /// <summary>
        /// Menambahkan kolom baru dengan konfigurasi praktis (Fluent API)
        /// </summary>
        public VirtualDataGridColumn AddColumn(string header, string bindingPath, double width = 100,
            ColumnType columnType = ColumnType.Text, bool isFrozen = false, string formatString = null,
            bool isVisible = true, TextAlignment textAlignment = TextAlignment.Left)
        {
            var column = new VirtualDataGridColumn(header, bindingPath, width, columnType)
            {
                IsFrozen = isFrozen,
                FormatString = formatString,
                IsVisible = isVisible,
                TextAlignment = textAlignment
            };

            Add(column);
            return column;
        }

        /// <summary>
        /// Menambahkan multiple kolom sekaligus
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
        /// Menghapus kolom berdasarkan binding path
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
        /// Set DisplayIndex memastikan konsistensi (urut berdasarkan collection order)
        /// </summary>
        public void NormalizeDisplayIndex()
        {
            for (int i = 0; i < Count; i++)
                this[i].DisplayIndex = i;
        }

        /// <summary>
        /// Mengurutkan kolom berdasarkan DisplayIndex
        /// </summary>
        public void SortByDisplayIndex()
        {
            var sorted = this.OrderBy(c => c.DisplayIndex).ToList();
            Clear();
            foreach (var column in sorted)
            {
                Add(column);
            }
        }

        /// <summary>
        /// Memindahkan kolom dari oldIndex ke newIndex
        /// </summary>
        public void MoveColumn(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;
            if (oldIndex < 0 || oldIndex >= Count)
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if (newIndex < 0 || newIndex >= Count)
                throw new ArgumentOutOfRangeException(nameof(newIndex));

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
        /// Auto-size semua kolom berdasarkan sample data
        /// </summary>
        public void AutoSizeAllColumns(IEnumerable<object> sampleItems = null, int sampleSize = 30, double fontSize = 12.0)
        {
            foreach (var column in this.Where(c => c.IsVisible))
            {
                column.AutoSize(sampleItems, sampleSize, null, fontSize);
            }
        }

        /// <summary>
        /// Show semua kolom
        /// </summary>
        public void ShowAllColumns()
        {
            foreach (var column in this)
            {
                column.IsVisible = true;
            }
        }

        /// <summary>
        /// Hide semua kolom
        /// </summary>
        public void HideAllColumns()
        {
            foreach (var column in this)
            {
                column.IsVisible = false;
            }
        }

        /// <summary>
        /// Show kolom berdasarkan binding path
        /// </summary>
        public void ShowColumn(string bindingPath)
        {
            var column = GetByBinding(bindingPath);
            if (column != null)
                column.IsVisible = true;
        }

        /// <summary>
        /// Hide kolom berdasarkan binding path
        /// </summary>
        public void HideColumn(string bindingPath)
        {
            var column = GetByBinding(bindingPath);
            if (column != null)
                column.IsVisible = false;
        }

        /// <summary>
        /// Toggle visibility kolom berdasarkan binding path
        /// </summary>
        public void ToggleColumnVisibility(string bindingPath)
        {
            var column = GetByBinding(bindingPath);
            if (column != null)
                column.IsVisible = !column.IsVisible;
        }

        /// <summary>
        /// Freeze kolom berdasarkan binding path
        /// </summary>
        public void FreezeColumn(string bindingPath)
        {
            var column = GetByBinding(bindingPath);
            if (column != null)
            {
                column.IsFrozen = true;
                EnforceFrozenColumnsAtFront();
            }
        }

        /// <summary>
        /// Unfreeze kolom berdasarkan binding path
        /// </summary>
        public void UnfreezeColumn(string bindingPath)
        {
            var column = GetByBinding(bindingPath);
            if (column != null)
                column.IsFrozen = false;
        }

        /// <summary>
        /// Reset semua kolom ke state default
        /// </summary>
        public void ResetToDefault()
        {
            foreach (var column in this)
            {
                column.IsVisible = true;
                column.IsFrozen = false;
                column.SortDirection = null;
                column.Width = Math.Max(column.MinWidth, 100); // Reset ke default yang reasonable
            }
        }

        /// <summary>
        /// Reset ke default columns berdasarkan entity type (auto-generate)
        /// </summary>
        public void ResetToDefault(Type entityType)
        {
            Clear();

            if (entityType != null)
            {
                var autoColumns = ColumnAutoGenerator.GenerateColumnsFromType(entityType);
                AddRange(autoColumns);
            }
        }

        ///// <summary>
        ///// Get core configs untuk data processing pipeline
        ///// Lightweight POCO untuk performa tinggi
        ///// </summary>
        //public ColumnConfig[] GetCoreConfigs()
        //{
        //    return this.Where(column => column.IsVisible)
        //              .OrderBy(column => column.DisplayIndex >= 0 ? column.DisplayIndex : IndexOf(column))
        //              .Select(column => new ColumnConfig(
        //                  bindingPath: column.BindingPath,
        //                  columnType: column.ColumnType,
        //                  formatString: column.FormatString,
        //                  isSummary: column.IsSummary,
        //                  summaryType: column.SummaryType
        //              )).ToArray();
        //}

        ///// <summary>
        ///// Get snapshots untuk rendering system
        ///// Immutable snapshot untuk consistency selama render cycle
        ///// </summary>
        //public ColumnSnapshot[] GetSnapshots()
        //{
        //    return this.Where(column => column.IsVisible)
        //              .OrderBy(column => column.DisplayIndex >= 0 ? column.DisplayIndex : IndexOf(column))
        //              .Select(column => new ColumnSnapshot(
        //                  header: column.Header,
        //                  width: column.Width,
        //                  isFrozen: column.IsFrozen,
        //                  textAlignment: column.TextAlignment,
        //                  columnType: column.ColumnType,
        //                  isVisible: column.IsVisible,
        //                  displayIndex: column.DisplayIndex >= 0 ? column.DisplayIndex : IndexOf(column),
        //                  cellTemplate: column.CellTemplate,
        //                  headerTemplate: column.HeaderTemplate
        //              )).ToArray();
        //}


        /// <summary>
        /// Clone collection beserta semua kolomnya (deep clone)
        /// </summary>
        public ColumnCollection Clone()
        {
            var newCollection = new ColumnCollection();
            foreach (var column in this)
            {
                newCollection.Add(column.Clone());
            }
            return newCollection;
        }

        /// <summary>
        /// Mendapatkan binding paths dari semua kolom
        /// </summary>
        public IEnumerable<string> GetBindingPaths()
        {
            return this.Select(c => c.BindingPath).Where(path => !string.IsNullOrEmpty(path));
        }

        /// <summary>
        /// Mendapatkan binding paths dari kolom yang visible
        /// </summary>
        public IEnumerable<string> GetVisibleBindingPaths()
        {
            return this.Where(c => c.IsVisible).Select(c => c.BindingPath).Where(path => !string.IsNullOrEmpty(path));
        }

        #endregion

        #region Event Handlers & Private Methods

        /// <summary>
        /// Handler untuk collection changed
        /// </summary>
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateColumnMap(e);
            RecalculateDisplayIndexes();

            if (!_isReordering)
            {
                ColumnsChanged?.Invoke(this, EventArgs.Empty);
            }

            // Notify calculated properties changed
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalWidth)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalVisibleWidth)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(FrozenWidth)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(FrozenColumnCount)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(VisibleColumnCount)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasFrozenColumns)));
        }

        /// <summary>
        /// Update dictionary mapping ketika collection berubah
        /// </summary>
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

                case NotifyCollectionChangedAction.Replace:
                    foreach (VirtualDataGridColumn column in e.OldItems)
                    {
                        if (!string.IsNullOrEmpty(column.BindingPath))
                        {
                            _columnMap.Remove(column.BindingPath);
                        }
                        column.PropertyChanged -= OnColumnPropertyChanged;
                    }
                    foreach (VirtualDataGridColumn column in e.NewItems)
                    {
                        if (!string.IsNullOrEmpty(column.BindingPath))
                        {
                            _columnMap[column.BindingPath] = column;
                        }
                        column.PropertyChanged += OnColumnPropertyChanged;
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
            }
        }

        /// <summary>
        /// Handler untuk property changed pada kolom individual
        /// </summary>
        private void OnColumnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var column = (VirtualDataGridColumn)sender;

            // Update mapping jika BindingPath berubah
            if (e.PropertyName == nameof(VirtualDataGridColumn.BindingPath))
            {
                // Rebuild seluruh mapping untuk konsistensi
                _columnMap.Clear();
                foreach (var col in this)
                {
                    if (!string.IsNullOrEmpty(col.BindingPath))
                    {
                        _columnMap[col.BindingPath] = col;
                    }
                }
            }

            // Enforce frozen columns di depan ketika kolom di-freeze
            if (e.PropertyName == nameof(VirtualDataGridColumn.IsFrozen) && column.IsFrozen)
            {
                EnforceFrozenColumnsAtFront();
            }

            // Notify calculated properties jika property yang berubah mempengaruhinya
            if (e.PropertyName == nameof(VirtualDataGridColumn.Width) ||
                e.PropertyName == nameof(VirtualDataGridColumn.IsVisible) ||
                e.PropertyName == nameof(VirtualDataGridColumn.IsFrozen))
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalWidth)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalVisibleWidth)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(FrozenWidth)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(FrozenColumnCount)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(VisibleColumnCount)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasFrozenColumns)));
            }

            ColumnPropertyChanged?.Invoke(this, new ColumnPropertyChangedEventArgs(column, e.PropertyName));
        }

        /// <summary>
        /// Recalculate display indexes berdasarkan urutan collection
        /// </summary>
        private void RecalculateDisplayIndexes()
        {
            for (int i = 0; i < Count; i++)
            {
                this[i].DisplayIndex = i;
            }
        }

        /// <summary>
        /// Pastikan frozen columns selalu berada di depan
        /// </summary>
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

        /// <summary>
        /// Event ketika property pada kolom berubah
        /// </summary>
        public event EventHandler<ColumnPropertyChangedEventArgs> ColumnPropertyChanged;

        /// <summary>
        /// Event ketika collection berubah (tambah/hapus kolom)
        /// </summary>
        public event EventHandler ColumnsChanged;

        /// <summary>
        /// Event ketika kolom di-reorder
        /// </summary>
        public event EventHandler<ColumnsReorderedEventArgs> ColumnsReordered;

        #endregion
    }

    /// <summary>
    /// Static helper untuk auto-column generation
    /// </summary>
    public static class ColumnAutoGenerator
    {
        /// <summary>
        /// Generate columns otomatis dari entity type
        /// </summary>
        public static ColumnCollection GenerateColumnsFromType(Type entityType)
        {
            var collection = new ColumnCollection();

            if (entityType == null) return collection;

            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.CanRead && IsSupportedType(p.PropertyType));

            foreach (var prop in properties)
            {
                var column = new VirtualDataGridColumn
                {
                    Header = FormatHeader(prop.Name),
                    BindingPath = prop.Name,
                    Width = GetDefaultWidth(prop.PropertyType),
                    ColumnType = GetColumnType(prop.PropertyType),
                    FormatString = GetDefaultFormatString(prop.PropertyType),
                    TextAlignment = GetDefaultTextAlignment(prop.PropertyType)
                };

                collection.Add(column);
            }

            return collection;
        }

        /// <summary>
        /// Generate columns otomatis dari sample object
        /// </summary>
        public static ColumnCollection GenerateColumnsFromObject(object sampleObject)
        {
            if (sampleObject == null)
                return new ColumnCollection();

            return GenerateColumnsFromType(sampleObject.GetType());
        }

        private static string FormatHeader(string propertyName)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                propertyName,
                "([a-z])([A-Z])",
                "$1 $2"
            );
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
                Type t when t.IsEnum => ColumnType.ComboBox,
                _ => ColumnType.Text
            };
        }

        private static string GetDefaultFormatString(Type propertyType)
        {
            return propertyType switch
            {
                Type t when t == typeof(decimal) || t == typeof(double) || t == typeof(float) => "N2",
                Type t when t == typeof(DateTime) => "dd/MM/yyyy",
                _ => null
            };
        }

        private static TextAlignment GetDefaultTextAlignment(Type propertyType)
        {
            return propertyType switch
            {
                Type t when t == typeof(string) => TextAlignment.Left,
                Type t when t == typeof(int) || t == typeof(long) ||
                           t == typeof(decimal) || t == typeof(double) || t == typeof(float) => TextAlignment.Right,
                Type t when t == typeof(DateTime) => TextAlignment.Center,
                Type t when t == typeof(bool) => TextAlignment.Center,
                _ => TextAlignment.Left
            };
        }

        private static bool IsSupportedType(Type type)
        {
            return type == typeof(string) || type == typeof(int) || type == typeof(long) ||
                   type == typeof(decimal) || type == typeof(double) || type == typeof(float) ||
                   type == typeof(DateTime) || type == typeof(bool) || type.IsEnum ||
                   type == typeof(short) || type == typeof(byte) || type == typeof(DateTimeOffset);
        }
    }
}