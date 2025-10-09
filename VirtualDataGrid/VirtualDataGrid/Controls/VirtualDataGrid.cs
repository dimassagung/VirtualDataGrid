using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VirtualDataGrid.Core;
using VirtualDataGrid.Data;
using VirtualDataGrid.Managers;

namespace VirtualDataGrid.Controls
{
    [TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
    [TemplatePart(Name = "PART_RenderCanvas", Type = typeof(Canvas))]
    [TemplatePart(Name = "PART_HeaderPanel", Type = typeof(Panel))]
    [TemplatePart(Name = "PART_VirtualizingPanel", Type = typeof(VirtualizingPanel))]
    public class VirtualDataGrid : Control, INotifyPropertyChanged
    {
        #region Dependency Properties
        public static readonly DependencyProperty ItemsSourceProperty =
          DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(VirtualDataGrid),
              new FrameworkPropertyMetadata(null, OnItemsSourceChanged, CoerceItemsSource));

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(ColumnCollection), typeof(VirtualDataGrid),
                new FrameworkPropertyMetadata(new ColumnCollection(), OnColumnsChanged));

        public static readonly DependencyProperty SelectedItemProperty =
         DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(VirtualDataGrid),
         new FrameworkPropertyMetadata(null, OnSelectedItemChanged));

        public static readonly DependencyProperty SelectedItemsProperty =
       DependencyProperty.Register(nameof(SelectedItems), typeof(IList), typeof(VirtualDataGrid),
           new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SelectionModeProperty =
           DependencyProperty.Register(nameof(SelectionMode), typeof(Core.SelectionMode), typeof(VirtualDataGrid),
               new FrameworkPropertyMetadata(Core.SelectionMode.Single));

        public static readonly DependencyProperty FilterTextProperty =
          DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(VirtualDataGrid),
              new FrameworkPropertyMetadata(null, OnFilterTextChanged));

        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(VirtualDataGrid),
                new FrameworkPropertyMetadata(25.0));
        //  new FrameworkPropertyMetadata(VirtualGridConfig.DefaultRowHeight));

        public static readonly DependencyProperty FrozenColumnCountProperty =
            DependencyProperty.Register(nameof(FrozenColumnCount), typeof(int), typeof(VirtualDataGrid),
                new FrameworkPropertyMetadata(0));

        public static readonly DependencyProperty IsReadOnlyProperty =
         DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(VirtualDataGrid),
             new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty AlternationCountProperty =
            DependencyProperty.Register(nameof(AlternationCount), typeof(int), typeof(VirtualDataGrid),
                new FrameworkPropertyMetadata(2));
        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register(nameof(Theme), typeof(string), typeof(VirtualDataGrid),
                new FrameworkPropertyMetadata("Light", OnThemeChanged));

        // Events
        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectionChanged), RoutingStrategy.Bubble,
                typeof(SelectionChangedEventHandler), typeof(VirtualDataGrid));

        public static readonly RoutedEvent RowDoubleClickEvent =
            EventManager.RegisterRoutedEvent(nameof(RowDoubleClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(VirtualDataGrid));

        public static readonly RoutedEvent CellClickEvent =
            EventManager.RegisterRoutedEvent(nameof(CellClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(VirtualDataGrid));

        #endregion Dependency Properties

        #region Internal Components
        private readonly BackgroundProcessor<object> _backgroundProcessor;
        private readonly UltraCrudPipeline<object> _pipeline;
        private readonly DataConverter<object> _converter;
        //private readonly VirtualizedRenderer _renderer;

        private readonly SelectionManager _selectionManager;
        public SelectionManager SelectionManager => _selectionManager;

        //private readonly InteractionManager _interactionManager;
        //private readonly FilterSortManager _filterSortManager;
        //private readonly ColumnManager _columnManager;
        //private readonly ThemeManager _themeManager;
        //private readonly PerformanceMonitor _performanceMonitor;

        private ScrollViewer _scrollViewer;
        private Canvas _renderCanvas;
        private Panel _headerPanel;
        private bool _isTemplateApplied;
        private double _totalHeight;
        #endregion

        #region Public Properties
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public ColumnCollection Columns
        {
            get => (ColumnCollection)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        public Core.SelectionMode SelectionMode
        {
            get => (Core.SelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public int FrozenColumnCount
        {
            get => (int)GetValue(FrozenColumnCountProperty);
            set => SetValue(FrozenColumnCountProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public int AlternationCount
        {
            get => (int)GetValue(AlternationCountProperty);
            set => SetValue(AlternationCountProperty, value);
        }

        public string Theme
        {
            get => (string)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        public int TotalRowCount { get; private set; }
        public ScrollViewer ScrollViewer => _scrollViewer;
        public double TotalHeight => _totalHeight;

        #endregion

        #region Events
        public event SelectionChangedEventHandler SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        public event RoutedEventHandler RowDoubleClick
        {
            add => AddHandler(RowDoubleClickEvent, value);
            remove => RemoveHandler(RowDoubleClickEvent, value);
        }

        public event RoutedEventHandler CellClick
        {
            add => AddHandler(CellClickEvent, value);
            remove => RemoveHandler(CellClickEvent, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        static VirtualDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VirtualDataGrid),
                new FrameworkPropertyMetadata(typeof(VirtualDataGrid)));
        }

        public VirtualDataGrid()
        {
            //// Initialize managers
            //_selectionManager = new SelectionManager(this);
            //_interactionManager = new InteractionManager(this);
            //_filterSortManager = new FilterSortManager(this);
            //_columnManager = new ColumnManager(this);
            //_themeManager = new ThemeManager(this);
            //_performanceMonitor = new PerformanceMonitor();

            //// Initialize pipeline and renderer
            _converter = new DataConverter<object>(Columns);
            _pipeline = new UltraCrudPipeline<object>(new ColumnCollection());
            _backgroundProcessor = new BackgroundProcessor<object>(Columns, _pipeline);
            //_renderer = new VirtualizedRenderer();

            //// Subscribe to events
            //SubscribeToEvents();

            //Loaded += OnLoaded;
            //Unloaded += OnUnloaded;
        }

        private void SubscribeToEvents()
        {
            //  Selection events
            //_selectionManager.SelectionChanged += OnSelectionChangedInternal;

            //// Interaction events
            //_interactionManager.RowHoverChanged += OnRowHoverChanged;
            //_interactionManager.RowClick += OnRowClick;
            //_interactionManager.CellClick += OnCellClick;
            //_interactionManager.KeyboardNavigation += OnKeyboardNavigation;
            //_interactionManager.ColumnResizeChanged += OnColumnResizeChanged;

            // Filter/Sort events
            //    _filterSortManager.FilterApplied += OnFilterApplied;
            //    _filterSortManager.SortApplied += OnSortApplied;

            // Pipeline events
            //_pipeline.DataUpdated += OnPipelineDataUpdated;

            // Column events
            Columns.CollectionChanged += OnColumnsCollectionChanged;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            _renderCanvas = GetTemplateChild("PART_RenderCanvas") as Canvas;
            _headerPanel = GetTemplateChild("PART_HeaderPanel") as Panel;

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }

            if (_headerPanel != null)
            {
                InitializeHeaderPanel();
            }

            _isTemplateApplied = true;
            InvalidateMeasure();
        }

        private void InitializeHeaderPanel()
        {
            _headerPanel.Children.Clear();

            //foreach (var column in Columns)
            //{
            //    var header = new ColumnHeader
            //    {
            //        Content = column.Header,
            //        Width = column.Width,
            //        Column = column
            //    };

            //    header.MouseLeftButtonDown += OnHeaderMouseLeftButtonDown;
            //    _headerPanel.Children.Add(header);
            //}
        }


        #region Event Handlers
        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (VirtualDataGrid)d;
            grid.OnItemsSourceChanged(e.OldValue as IEnumerable, e.NewValue as IEnumerable);
        }

        private static object CoerceItemsSource(DependencyObject d, object baseValue)
        {
            return baseValue ?? Array.Empty<object>();
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (VirtualDataGrid)d;
            grid.OnColumnsChanged(e.OldValue as ColumnCollection, e.NewValue as ColumnCollection);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (VirtualDataGrid)d;
            grid.OnSelectedItemChanged(e.OldValue, e.NewValue);
        }

        private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (VirtualDataGrid)d;
            grid.OnFilterTextChanged(e.OldValue as string, e.NewValue as string);
        }

        private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (VirtualDataGrid)d;
            grid.OnThemeChanged(e.OldValue as string, e.NewValue as string);
        }

        private void OnItemsSourceChanged(IEnumerable oldSource, IEnumerable newSource)
        {
            if (newSource == null) return;

            // Convert and push data to pipeline
            var typedData = newSource.Cast<object>().ToList();
            TotalRowCount = typedData.Count;

            var bindings = Columns.Select(c => c.BindingPath).ToArray();
            //// Build converter (object-based via reflection)
            //_converter?.Dispose();
            // _converter = new DataConverter<object>(bindings);
           // var internalData = _converter.ConvertToInternal(newSource, Columns);
            //_pipeline.PushData(internalData);
            //_pipeline.PushData(typedData, _converter);

            UpdateTotalHeight();

            // Unsubscribe from old collection if it's observable
            if (oldSource is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= OnCollectionChanged;

            // Subscribe to new collection
            if (newSource is INotifyCollectionChanged newCollection)
                newCollection.CollectionChanged += OnCollectionChanged;

        }
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Translate collection changes to DataUpdate batches
            // This is a simplified example; need to handle Add, Remove, Replace, Reset
            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems)
                {
                    //// Convert newItem to DataRow and push update
                    //var row = ConvertToDataRow(newItem);
                    //_pipeline.PushUpdate(new DataUpdate { Type = UpdateType.Add, RowData = row });
                }
            }
            // ... similar for other change types
        }

        private void UpdateConverter()
        {
            //// ✅ PRIORITAS: Columns > BindingPaths
            //if (Columns?.Any() == true)
            //{
            //    _converter = new DataConverter(Columns);
            //}
            //else if (BindingPaths?.Any() == true)
            //{
            //    _converter = new DataConverter(BindingPaths);
            //}
            //else
            //{
            //    // Fallback: auto-detect dari data nanti
            //    _converter = new DataConverter(Array.Empty<string>());
            //}
        }
        private void OnColumnsChanged(ColumnCollection oldColumns, ColumnCollection newColumns)
        {
            if (oldColumns != null)
            {
                oldColumns.CollectionChanged -= OnColumnsCollectionChanged;
            }

            newColumns.CollectionChanged += OnColumnsCollectionChanged;

            // Reinitialize header panel
            if (_headerPanel != null)
            {
                InitializeHeaderPanel();
            }

            InvalidateVisual();
        }

        private void OnSelectedItemChanged(object oldItem, object newItem)
        {
            // Find row index for the selected item and select it
            if (newItem != null)
            {
                // This would need to be implemented based on your data structure
                // For now, just invalidate visual to show selection
                InvalidateVisual();
            }
        }

        private void OnFilterTextChanged(string oldFilter, string newFilter)
        {
            // _filterSortManager.ApplyFilter(newFilter);
        }

        private void OnThemeChanged(string oldTheme, string newTheme)
        {
            // _themeManager.SwitchTheme(newTheme);
        }


        //private void OnSelectionChangedInternal(object sender, Managers.SelectionChangedEventArgs e)
        //{
        //    // Update dependency properties
        //    UpdateSelectionProperties();

        //    // Raise routed event
        //    var args = new System.Windows.Controls.SelectionChangedEventArgs(
        //        SelectionChangedEvent, (IList)e.RemovedIndices, (IList)e.AddedIndices);
        //    RaiseEvent(args);

        //    InvalidateVisual();
        //}

        //private void OnRowHoverChanged(object sender, RowHoverEventArgs e)
        //{
        //    InvalidateVisual(); // Only hover area should be invalidated for performance
        //}

        //private void OnRowClick(object sender, RowClickEventArgs e)
        //{
        //    var args = new RoutedEventArgs(RowDoubleClickEvent);
        //    RaiseEvent(args);
        //}

        //private void OnCellClick(object sender, CellClickEventArgs e)
        //{
        //    var args = new RoutedEventArgs(CellClickEvent);
        //    RaiseEvent(args);
        //}

        //private void OnKeyboardNavigation(object sender, KeyboardNavEventArgs e)
        //{
        //    // Allow custom keyboard handling
        //    if (!e.Handled)
        //    {
        //        // Default keyboard handling
        //        switch (e.Key)
        //        {
        //            case Key.Up: _selectionManager.MoveSelection(-1); break;
        //            case Key.Down: _selectionManager.MoveSelection(1); break;
        //            case Key.PageUp: _selectionManager.MoveSelection(-CalculatePageSize()); break;
        //            case Key.PageDown: _selectionManager.MoveSelection(CalculatePageSize()); break;
        //            case Key.Home: _selectionManager.SelectFirst(); break;
        //            case Key.End: _selectionManager.SelectLast(); break;
        //        }
        //    }
        //}

        //private void OnColumnResizeChanged(object sender, ColumnResizeEventArgs e)
        //{
        //    //  _columnManager.ResizeColumn(e.ColumnIndex, e.NewWidth);
        //    InvalidateVisual();
        //}

        //private void OnFilterApplied(object sender, FilterAppliedEventArgs e)
        //{
        //    //_pipeline.ApplyFilter(e.FilterText);
        //}

        //private void OnSortApplied(object sender, SortAppliedEventArgs e)
        //{
        //    // Apply sort to pipeline
        //    foreach (var sort in e.SortDescriptions)
        //    {
        //        //  _pipeline.ApplySort(sort);
        //    }
        //}

        //private void OnPipelineDataUpdated(object sender, DataUpdatedEventArgs e)
        //{
        //    //Dispatcher.BeginInvoke(() =>
        //    //{
        //    //    //TotalRowCount = (int)(_pipeline.GetVisibleData().Length * 1.2); // Estimate
        //    //    //UpdateTotalHeight();
        //    //    //InvalidateVisual();
        //    //});
        //}

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 || e.HorizontalChange != 0)
            {
                InvalidateVisual();
            }
        }

        private void OnColumnsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_headerPanel != null)
            {
                InitializeHeaderPanel();
            }
            InvalidateVisual();
        }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if (sender is ColumnHeader header)
            //{
            //    //_filterSortManager.ToggleSort(header.Column.BindingPath);
            //}
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initial render
            InvalidateVisual();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup
            //_pipeline?.Dispose();
            //_renderer?.Dispose();
            //_performanceMonitor?.Dispose();
        }
        #endregion



        #region Rendering
        protected override void OnRender(DrawingContext dc)
        {
            if (!_isTemplateApplied || Columns.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                base.OnRender(dc);
                return;
            }

        }

        protected override Size MeasureOverride(Size constraint)
        {
            UpdateTotalHeight();
            return new Size(constraint.Width, Math.Min(constraint.Height, _totalHeight));
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            UpdateTotalHeight();
            return base.ArrangeOverride(arrangeBounds);
        }

        private void UpdateTotalHeight()
        {
            //    _totalHeight = TotalRowCount * RowHeight;
            //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalHeight)));
        }
        #endregion

        #region Public API Methods
        public void Refresh()
        {
            InvalidateVisual();
        }

        public void ScrollToRow(int rowIndex)
        {
            //   _interactionManager.ScrollToRow(rowIndex);
        }

        public void ScrollToItem(object item)
        {
            // Find row index for item and scroll to it
            // Implementation depends on data structure
        }

        public object GetCellValue(int rowIndex, int columnIndex)
        {
            //var visibleData = _pipeline.GetVisibleData();
            //if (rowIndex >= 0 && rowIndex < visibleData.Length &&
            //    columnIndex >= 0 && columnIndex < Columns.Count)
            //{
            //    return visibleData[rowIndex].Cells[columnIndex];
            //}
            return null;
        }

        public object GetRowData(int rowIndex)
        {
            //var visibleData = _pipeline.GetVisibleData();
            //if (rowIndex >= 0 && rowIndex < visibleData.Length)
            //{
            //    return visibleData[rowIndex].OriginalItem;
            //}
            return null;
        }

        public void ApplyFilter(string filterText)
        {
            FilterText = filterText;
        }

        public void ClearFilter()
        {
            FilterText = null;
        }

        public void SortBy(string columnName, ListSortDirection direction = ListSortDirection.Ascending)
        {
            //    _filterSortManager.SortBy(columnName, direction);
        }

        public void ClearSort()
        {
            // _filterSortManager.ClearSort();
        }

        public void SelectAll()
        {
            // _selectionManager.SelectAll();
        }

        public void ClearSelection()
        {
            // _selectionManager.ClearSelection();
        }

        public void AutoSizeColumns()
        {
            // _columnManager.AutoSizeAllColumns();
        }

        public void SwitchTheme(string themeName)
        {
            Theme = themeName;
        }
        #endregion


        #region Helper Methods
        private void UpdateSelectionProperties()
        {
            //var selectedIndices = _selectionManager.GetSelectedIndices().ToList();

            //if (selectedIndices.Count == 1)
            //{
            //    SelectedItem = GetRowData(selectedIndices[0]);
            //}
            //else
            //{
            //    SelectedItem = null;
            //}

            //// Update SelectedItems collection
            //var selectedItems = selectedIndices.Select(GetRowData).ToList();
            //SelectedItems = selectedItems;
        }

        private int CalculatePageSize()
        {
            //var viewportHeight = _scrollViewer?.ViewportHeight ?? ActualHeight;
            //return (int)(viewportHeight / RowHeight);
            return 100;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}