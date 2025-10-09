using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VirtualDataGrid.Controls
{
    // <summary>
    /// Column definition untuk UI - digunakan di XAML dan VirtualDataGrid control
    /// </summary>
    public class VirtualDataGridColumn : DependencyObject, INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(VirtualDataGridColumn),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static readonly DependencyProperty BindingPathProperty =
            DependencyProperty.Register(nameof(BindingPath), typeof(string), typeof(VirtualDataGridColumn),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.Register(nameof(Width), typeof(double), typeof(VirtualDataGridColumn),
                new PropertyMetadata(120.0, OnPropertyChanged));

        public static readonly DependencyProperty MinWidthProperty =
            DependencyProperty.Register(nameof(MinWidth), typeof(double), typeof(VirtualDataGridColumn),
                new PropertyMetadata(50.0, OnPropertyChanged));

        public static readonly DependencyProperty MaxWidthProperty =
            DependencyProperty.Register(nameof(MaxWidth), typeof(double), typeof(VirtualDataGridColumn),
                new PropertyMetadata(500.0, OnPropertyChanged));

        public static readonly DependencyProperty FormatStringProperty =
            DependencyProperty.Register(nameof(FormatString), typeof(string), typeof(VirtualDataGridColumn),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register(nameof(TextAlignment), typeof(TextAlignment), typeof(VirtualDataGridColumn),
                new PropertyMetadata(TextAlignment.Left, OnPropertyChanged));

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(VirtualDataGridColumn),
                new PropertyMetadata(true, OnPropertyChanged));

        public static readonly DependencyProperty IsFrozenProperty =
            DependencyProperty.Register(nameof(IsFrozen), typeof(bool), typeof(VirtualDataGridColumn),
                new PropertyMetadata(false, OnPropertyChanged));

        public static readonly DependencyProperty SortDirectionProperty =
            DependencyProperty.Register(nameof(SortDirection), typeof(ListSortDirection?), typeof(VirtualDataGridColumn),
                new PropertyMetadata(null, OnPropertyChanged));

        public static readonly DependencyProperty ColumnTypeProperty =
            DependencyProperty.Register(nameof(ColumnType), typeof(ColumnType), typeof(VirtualDataGridColumn),
                new PropertyMetadata(ColumnType.Text, OnPropertyChanged));

        public static readonly DependencyProperty IsSummaryProperty =
            DependencyProperty.Register(nameof(IsSummary), typeof(bool), typeof(VirtualDataGridColumn),
                new PropertyMetadata(false, OnPropertyChanged));

        public static readonly DependencyProperty SummaryTypeProperty =
            DependencyProperty.Register(nameof(SummaryType), typeof(SummaryType), typeof(VirtualDataGridColumn),
                new PropertyMetadata(SummaryType.None, OnPropertyChanged));

        public static readonly DependencyProperty CellTemplateProperty =
            DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(VirtualDataGridColumn),
                new PropertyMetadata(null, OnPropertyChanged));

        public static readonly DependencyProperty DisplayIndexProperty =
            DependencyProperty.Register(nameof(DisplayIndex), typeof(int), typeof(VirtualDataGridColumn),
                new PropertyMetadata(-1, OnPropertyChanged));

        #endregion

        #region CLR Properties

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string BindingPath
        {
            get => (string)GetValue(BindingPathProperty);
            set => SetValue(BindingPathProperty, value);
        }

        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public double MinWidth
        {
            get => (double)GetValue(MinWidthProperty);
            set => SetValue(MinWidthProperty, value);
        }

        public double MaxWidth
        {
            get => (double)GetValue(MaxWidthProperty);
            set => SetValue(MaxWidthProperty, value);
        }

        public string FormatString
        {
            get => (string)GetValue(FormatStringProperty);
            set => SetValue(FormatStringProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public bool IsFrozen
        {
            get => (bool)GetValue(IsFrozenProperty);
            set => SetValue(IsFrozenProperty, value);
        }

        public ListSortDirection? SortDirection
        {
            get => (ListSortDirection?)GetValue(SortDirectionProperty);
            set => SetValue(SortDirectionProperty, value);
        }

        public ColumnType ColumnType
        {
            get => (ColumnType)GetValue(ColumnTypeProperty);
            set => SetValue(ColumnTypeProperty, value);
        }

        public bool IsSummary
        {
            get => (bool)GetValue(IsSummaryProperty);
            set => SetValue(IsSummaryProperty, value);
        }

        public SummaryType SummaryType
        {
            get => (SummaryType)GetValue(SummaryTypeProperty);
            set => SetValue(SummaryTypeProperty, value);
        }

        public DataTemplate CellTemplate
        {
            get => (DataTemplate)GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }

        public int DisplayIndex
        {
            get => (int)GetValue(DisplayIndexProperty);
            set => SetValue(DisplayIndexProperty, value);
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirtualDataGridColumn column)
            {
                column.PropertyChanged?.Invoke(column, new PropertyChangedEventArgs(e.Property.Name));
            }
        }

        #endregion

        #region Constructors

        public VirtualDataGridColumn() { }

        public VirtualDataGridColumn(string header, string bindingPath, double width = 120,
            ColumnType columnType = ColumnType.Text)
        {
            Header = header;
            BindingPath = bindingPath;
            Width = width;
            ColumnType = columnType;
        }

        #endregion

        #region Helper Methods

        public VirtualDataGridColumn Clone()
        {
            return new VirtualDataGridColumn
            {
                Header = Header,
                BindingPath = BindingPath,
                Width = Width,
                MinWidth = MinWidth,
                MaxWidth = MaxWidth,
                FormatString = FormatString,
                TextAlignment = TextAlignment,
                IsVisible = IsVisible,
                IsFrozen = IsFrozen,
                SortDirection = SortDirection,
                ColumnType = ColumnType,
                IsSummary = IsSummary,
                SummaryType = SummaryType,
                CellTemplate = CellTemplate,
                DisplayIndex = DisplayIndex
            };
        }

        public void AutoSize(IEnumerable<object> sampleItems = null, int sampleSize = 30,
            Func<object, string> valueFormatter = null, double fontSize = 12.0)
        {
            // Simple auto-size logic
            var headerWidth = CalculateTextWidth(Header, fontSize) + 20; // Padding
            double contentWidth = headerWidth;

            if (sampleItems != null)
            {
                var samples = sampleItems.Take(sampleSize);
                foreach (var item in samples)
                {
                    var value = GetValueFromItem(item);
                    var text = valueFormatter?.Invoke(value) ?? value?.ToString() ?? string.Empty;
                    var textWidth = CalculateTextWidth(text, fontSize) + 10; // Padding
                    contentWidth = Math.Max(contentWidth, textWidth);
                }
            }

            Width = Math.Max(MinWidth, Math.Min(MaxWidth, Math.Max(headerWidth, contentWidth)));
        }

        private object GetValueFromItem(object item)
        {
            if (item == null || string.IsNullOrEmpty(BindingPath)) return null;

            try
            {
                var current = item;
                foreach (var part in BindingPath.Split('.'))
                {
                    var prop = current.GetType().GetProperty(part);
                    if (prop == null) return null;
                    current = prop.GetValue(current);
                    if (current == null) break;
                }
                return current;
            }
            catch
            {
                return null;
            }
        }

        private double CalculateTextWidth(string text, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // Simple calculation - in real implementation, use FormattedText
            return text.Length * fontSize * 0.6; // Approximate
        }

        public override string ToString()
        {
            return $"{Header} ({BindingPath}) [{Width}px, Type={ColumnType}]";
        }

        #endregion
    }

    public enum ColumnType
    {
        Text,
        Number,
        Date,
        CheckBox,
        ComboBox,
        Template
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