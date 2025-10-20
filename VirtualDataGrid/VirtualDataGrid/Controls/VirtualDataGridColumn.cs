using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VirtualDataGrid.Controls
{
    /// <summary>
    /// VirtualDataGridColumn: definisi kolom untuk VirtualDataGrid control (XAML-friendly).
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
                new PropertyMetadata(30.0, OnPropertyChanged));

        public static readonly DependencyProperty MaxWidthProperty =
            DependencyProperty.Register(nameof(MaxWidth), typeof(double), typeof(VirtualDataGridColumn),
                new PropertyMetadata(double.PositiveInfinity, OnPropertyChanged));

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

        public static readonly DependencyProperty DisplayIndexProperty =
            DependencyProperty.Register(nameof(DisplayIndex), typeof(int), typeof(VirtualDataGridColumn),
                new PropertyMetadata(-1, OnPropertyChanged));

        public static readonly DependencyProperty CellTemplateProperty =
            DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(VirtualDataGridColumn),
                new PropertyMetadata(null, OnPropertyChanged));

        public static readonly DependencyProperty HeaderTemplateProperty =
            DependencyProperty.Register(nameof(HeaderTemplate), typeof(DataTemplate), typeof(VirtualDataGridColumn),
                new PropertyMetadata(null, OnPropertyChanged));

        #endregion

        #region CLR Properties (Dependency Property Wrappers)

        /// <summary>Text header yang tampil di UI.</summary>
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value ?? string.Empty);
        }

        /// <summary>Path property untuk binding (contoh: "Account.Symbol").</summary>
        public string BindingPath
        {
            get => (string)GetValue(BindingPathProperty);
            set => SetValue(BindingPathProperty, value ?? string.Empty);
        }

        /// <summary>Lebar kolom (pixel) — dapat diubah user via resize.</summary>
        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, Math.Max(0, value));
        }

        /// <summary>Lebar minimal kolom.</summary>
        public double MinWidth
        {
            get => (double)GetValue(MinWidthProperty);
            set => SetValue(MinWidthProperty, Math.Max(0, value));
        }

        /// <summary>Lebar maksimal kolom.</summary>
        public double MaxWidth
        {
            get => (double)GetValue(MaxWidthProperty);
            set => SetValue(MaxWidthProperty, Math.Max(0, value));
        }

        /// <summary>Format string untuk rendering (contoh "N2", "P1", "C0").</summary>
        public string FormatString
        {
            get => (string)GetValue(FormatStringProperty);
            set => SetValue(FormatStringProperty, value ?? string.Empty);
        }

        /// <summary>Alignment teks dalam cell.</summary>
        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        /// <summary>Visibility kolom (bisa disembunyikan oleh user/logic).</summary>
        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        /// <summary>Frozen flag — kolom ini akan di-fix pada bagian kiri.</summary>
        public bool IsFrozen
        {
            get => (bool)GetValue(IsFrozenProperty);
            set => SetValue(IsFrozenProperty, value);
        }

        /// <summary>Informasi arah sorting jika aktif (null bila tidak disort).</summary>
        public ListSortDirection? SortDirection
        {
            get => (ListSortDirection?)GetValue(SortDirectionProperty);
            set => SetValue(SortDirectionProperty, value);
        }

        /// <summary>Tipe kolom untuk membantu renderer memilih cell renderer/editor.</summary>
        public ColumnType ColumnType
        {
            get => (ColumnType)GetValue(ColumnTypeProperty);
            set => SetValue(ColumnTypeProperty, value);
        }

        /// <summary>Jika true, kolom ini diperlakukan sebagai kolom summary.</summary>
        public bool IsSummary
        {
            get => (bool)GetValue(IsSummaryProperty);
            set => SetValue(IsSummaryProperty, value);
        }

        /// <summary>Jenis summary (Sum/Average/Count/etc).</summary>
        public SummaryType SummaryType
        {
            get => (SummaryType)GetValue(SummaryTypeProperty);
            set => SetValue(SummaryTypeProperty, value);
        }

        /// <summary>Display index untuk pengurutan kolom.</summary>
        public int DisplayIndex
        {
            get => (int)GetValue(DisplayIndexProperty);
            set => SetValue(DisplayIndexProperty, value);
        }

        /// <summary>Template untuk cell (jika ColumnType == Template).</summary>
        public DataTemplate CellTemplate
        {
            get => (DataTemplate)GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }

        /// <summary>Template untuk header (opsional).</summary>
        public DataTemplate HeaderTemplate
        {
            get => (DataTemplate)GetValue(HeaderTemplateProperty);
            set => SetValue(HeaderTemplateProperty, value);
        }

        #endregion

        #region Additional CLR Properties (Fitur Baru dari Versi 2)

        /// <summary>
        /// Predicate untuk styling kondisional pada tingkat cell.
        /// - Diset oleh aplikasi (consumer control) jika ingin behavior dynamic coloring / highlight.
        /// - Tidak DISERIALISABLE ke XAML (delegate), gunakan di runtime.
        /// </summary>
        public CellStylePredicateDelegate? CellStylePredicate { get; set; }

        /// <summary>
        /// ToolTip generator (opsional): bisa jadi string static atau delegate untuk komposisi dinamis.
        /// </summary>
        public Func<object?, int, int, string?>? ToolTipProvider { get; set; }

        /// <summary>
        /// Optional tag/metadata user (mis. ID kolom bisnis, alias, dll).
        /// </summary>
        public object? Tag { get; set; }

        #endregion

        #region Events

        public event PropertyChangedEventHandler? PropertyChanged;

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

        #region Helper Methods (Gabungan dari Kedua Versi)

        /// <summary>
        /// Clone kolom — berguna saat membuat snapshot untuk renderer / pipeline.
        /// </summary>
        public VirtualDataGridColumn Clone()
        {
            return new VirtualDataGridColumn
            {
                Header = this.Header,
                BindingPath = this.BindingPath,
                Width = this.Width,
                MinWidth = this.MinWidth,
                MaxWidth = this.MaxWidth,
                FormatString = this.FormatString,
                TextAlignment = this.TextAlignment,
                IsVisible = this.IsVisible,
                IsFrozen = this.IsFrozen,
                SortDirection = this.SortDirection,
                ColumnType = this.ColumnType,
                IsSummary = this.IsSummary,
                SummaryType = this.SummaryType,
                DisplayIndex = this.DisplayIndex,
                CellTemplate = this.CellTemplate,
                HeaderTemplate = this.HeaderTemplate,
                // Copy additional CLR properties
                Tag = this.Tag,
                // Note: CellStylePredicate & ToolTipProvider are not copied intentionally
                // karena berupa delegate reference
                CellStylePredicate = this.CellStylePredicate,
                ToolTipProvider = this.ToolTipProvider
            };
        }

        /// <summary>
        /// Format value ke string berdasarkan FormatString jika ada.
        /// Renderer dapat memanggil ini sebelum menggambar teks.
        /// </summary>
        public string FormatValue(object? value)
        {
            if (value == null) return string.Empty;
            if (string.IsNullOrWhiteSpace(FormatString))
                return value.ToString() ?? string.Empty;

            try
            {
                // dukung format-alike string.Format("{0:...}", value)
                return string.Format(CultureInfo.CurrentCulture, "{0:" + FormatString + "}", value);
            }
            catch
            {
                // fallback aman
                return value.ToString() ?? string.Empty;
            }
        }

        /// <summary>
        /// Auto-size kolom berdasarkan sample data (dari versi 1).
        /// </summary>
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

        /// <summary>
        /// Mendapatkan nilai dari item berdasarkan BindingPath.
        /// </summary>
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

        /// <summary>
        /// Menghitung lebar teks secara approximate.
        /// </summary>
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




    #region Enumerations

    /// <summary>
    /// Tipe kolom untuk hint renderer / editor.
    /// </summary>
    public enum ColumnType
    {
        Text,
        Number,
        Date,
        CheckBox,
        ComboBox,
        Template,
        Button
    }

    /// <summary>
    /// Jenis summary yang didukung per kolom.
    /// Summary computation diserahkan ke SummaryManager / pipeline.
    /// </summary>
    public enum SummaryType
    {
        None,
        Sum,
        Average,
        Count,
        Min,
        Max,
        Avg
    }

    #endregion
    #region Helper Types

    /// <summary>
    /// Hasil style ringan untuk cell yang dikembalikan oleh predicate.
    /// Renderer akan menggabungkan style ini dengan default style kolom/grid.
    /// </summary>
    public sealed class CellStyle
    {
        public Brush? Foreground { get; set; }
        public Brush? Background { get; set; }
        public FontWeight? FontWeight { get; set; }
        public FontStyle? FontStyle { get; set; }
        public double? FontSize { get; set; }
        public Thickness? Padding { get; set; }

        public static readonly CellStyle Empty = new CellStyle();
    }

    /// <summary>
    /// Delegate untuk styling kondisional pada tingkat cell.
    /// Parameter:
    ///  - cellValue: nilai yang akan dirender (object)
    ///  - rowIndex: indeks baris (virtual index dalam dataset)
    ///  - columnIndex: indeks kolom (posisi kolom saat ini)
    ///  - column: referensi VirtualDataGridColumn
    /// Mengembalikan CellStyle atau null bila tidak ada perubahan style.
    /// </summary>
    public delegate CellStyle? CellStylePredicateDelegate(object? cellValue, int rowIndex, int columnIndex, VirtualDataGridColumn column);

    #endregion

}