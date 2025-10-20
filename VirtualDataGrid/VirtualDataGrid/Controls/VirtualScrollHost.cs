using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace VirtualDataGrid.Controls
{
    /// <summary>
    /// Kontrol pengganti ScrollViewer bawaan.
    /// - Menyimpan posisi scroll manual (OffsetX / OffsetY)
    /// - Memicu event ScrollChanged ringan
    /// - Tidak melayout ulang seluruh visual, hanya memberi tahu renderer & header
    /// </summary>
    [TemplatePart(Name = "PART_RenderCanvas", Type = typeof(Canvas))]
    public sealed class VirtualScrollHost : Control
    {
        private Canvas? _renderCanvas;
        private Point _scrollOffset;
        private Size _viewportSize;

        public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

        static VirtualScrollHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VirtualScrollHost),
                new FrameworkPropertyMetadata(typeof(VirtualScrollHost)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _renderCanvas = GetTemplateChild("PART_RenderCanvas") as Canvas;
        }

        /// <summary>Posisi scroll horizontal dalam piksel.</summary>
        public double HorizontalOffset
        {
            get => _scrollOffset.X;
            set
            {
                if (Math.Abs(value - _scrollOffset.X) > 0.1)
                {
                    _scrollOffset.X = Math.Max(value, 0);
                    RaiseScrollChanged();
                }
            }
        }

        /// <summary>Posisi scroll vertikal dalam piksel.</summary>
        public double VerticalOffset
        {
            get => _scrollOffset.Y;
            set
            {
                if (Math.Abs(value - _scrollOffset.Y) > 0.1)
                {
                    _scrollOffset.Y = Math.Max(value, 0);
                    RaiseScrollChanged();
                }
            }
        }

        /// <summary>Ukuran viewport saat ini.</summary>
        public Size ViewportSize
        {
            get => _viewportSize;
            set
            {
                if (_viewportSize != value)
                {
                    _viewportSize = value;
                    RaiseScrollChanged();
                }
            }
        }

        private void RaiseScrollChanged()
        {
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(_scrollOffset, _viewportSize));
            InvalidateVisual(); // minta renderer gambar ulang
        }

        // contoh event untuk drag scroll sederhana
        private Point _lastDrag;
        private bool _dragging;

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            _dragging = true;
            _lastDrag = e.GetPosition(this);
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            var pos = e.GetPosition(this);
            var dx = pos.X - _lastDrag.X;
            var dy = pos.Y - _lastDrag.Y;

            HorizontalOffset -= dx;
            VerticalOffset -= dy;

            _lastDrag = pos;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    public sealed class ScrollChangedEventArgs : EventArgs
    {
        public Point Offset { get; }
        public Size Viewport { get; }
        public int VerticalChange { get; internal set; }
        public int HorizontalChange { get; internal set; }

        public ScrollChangedEventArgs(Point offset, Size viewport)
        {
            Offset = offset;
            Viewport = viewport;
        }
    }
}