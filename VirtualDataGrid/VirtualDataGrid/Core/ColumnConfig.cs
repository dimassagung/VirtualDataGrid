using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VirtualDataGrid.Controls;

namespace VirtualDataGrid.Core
{
    /// ✅ UI LAYER - Rich features, XAML support
    //VirtualDataGridColumn : DependencyObject

    /// ✅ DATA LAYER - High performance, caching  
    //ColumnConfig : POCO

    //// ✅ RENDER LAYER - Fast, immutable, thread-safe
    //ColumnSnapshot : readonly struct
    //public class ColumnConfig
    //{
    //    public string BindingPath { get; set; }
    //    public ColumnType DataType { get; set; }
    //    public string FormatString { get; set; }

    //    public ColumnConfig(string path, ColumnType type)
    //    {
    //    }
    //}

    /// <summary>
    /// Column definition used by control/pipeline.
    /// Keep minimal: header, binding path, width, and summary flags.
    /// </summary>
    public class ColumnConfig
    {
        public string Header { get; set; } = string.Empty;
        public string BindingPath { get; set; } = string.Empty;
        public double Width { get; set; } = 120;


        // additional flags for rendering
        public bool IsFrozen { get; set; } = false;

        public bool IsSummary { get; set; } = false;
        public SummaryType SummaryType { get; set; } = SummaryType.None;
    }
}
