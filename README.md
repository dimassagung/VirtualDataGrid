VirtualGrid/
│
├─ Data/                         # Internal data pipeline & storage
│   ├─ DataRow.cs                 # Struct internal (fixed size, unmanaged)
│   ├─ SafeUnmanagedStore.cs      # Unmanaged memory manager
│   ├─ UltraCrudPipeline.cs       # Adaptive batching pipeline
│   ├─ DataUpdate.cs              # Update event model
│   └─ HybridStringPool.cs        # String optimization
│
├─ Rendering/                     # Pure rendering engine
│   ├─ VirtualGridRenderer.cs     # Main renderer (decoupled from Control)
│   ├─ FormattedTextCache.cs      # Text rendering cache
│   ├─ CellRenderer.cs            # Base cell renderer
│   ├─ TextCellRenderer.cs        # Text cell specialization
│   ├─ ButtonCellRenderer.cs      # Button cell specialization  
│   ├─ CheckboxCellRenderer.cs    # Checkbox cell specialization
│   ├─ RowRenderer.cs             # Row background, hover, selection
│   └─ GridLinesRenderer.cs       # Grid lines & frozen column visuals
│
├─ Controls/                      # WPF Controls (User-facing API)
│   ├─ VirtualDataGrid.cs         # Main control (DependencyObject)
│   ├─ VirtualDataGridColumn.cs   # Column definition
│   └─ ColumnDescriptor.cs        # Internal column config
│
├─ Templates/                     # XAML resources
│   ├─ Generic.xaml               # Default control template
│   └─ Themes/
│       ├─ LightTheme.xaml        # Light theme resources
│       └─ DarkTheme.xaml         # Dark theme resources
│
├─ Utils/                         # Utilities & helpers
│   ├─ LRUCache.cs                # Generic cache implementation
│   ├─ VisualHelper.cs            # DPI, visual tree helpers
│   └─ PerformanceCounter.cs      # Debug performance metrics
│
├─ Samples/                       # Demo & usage examples
│   ├─ MainWindow.xaml            # Demo window
│   ├─ MainWindow.xaml.cs         # Demo code-behind
│   └─ SampleDataGenerator.cs     # Test data generator
│
└─ VirtualGrid.csproj
