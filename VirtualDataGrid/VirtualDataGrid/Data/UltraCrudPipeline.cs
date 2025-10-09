using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Data
{
    /// <summary>
    /// Central pipeline orchestrator:
    /// - Accepts entity batches (via PushData)
    /// - Uses DataConverter to produce InternalRow[]
    /// - Writes to HybridDataStore
    /// - Maintains filter/sort cache and exposes visible rows
    /// - Publishes DataUpdated events
    /// </summary>
    //public class UltraCrudPipeline : IDisposable
    //OR ? kelebihgan kekuranggan
    public sealed class UltraCrudPipeline<T> : IDisposable where T : class
    {
        #region Private Fields
        private readonly Channel<InternalRow[]> _channel;
        //OR
        // private readonly Channel<UpdateBatch> _channel;

        private readonly DataConverter<T> _converter;
        private readonly HybridDataStore _dataStore;
        private readonly FilterSortEngine _filterSortEngine;
        //OR
        // private readonly FilterSortEngine<T> _filterSortEngine;

        //kenapa gak pernah di pake
        //private readonly BackgroundProcessor _bg;
        private readonly PerformanceMonitor _perf;
        private readonly object _cacheLock = new object();

        // cache of filtered result (materialized array)
        private InternalRow[] _cachedFiltered = Array.Empty<InternalRow>();
        //OR
        //private ReadOnlyMemory<InternalRow> _cachedFilteredData;
        private bool _filterCacheInvalid = true;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;
        private bool _disposed;
        #endregion


        public event EventHandler<DataUpdatedEventArgs>? DataUpdated;
        public event EventHandler<PipelineStatsEventArgs>? StatsUpdated;
        #region Constructor
        //public UltraCrudPipeline(ColumnCollection<T> columns, Dispatcher dispatcher) { ... }
        //public UltraCrudPipeline(
        // DataConverter<T> converter,
        // HybridDataStore store,
        // FilterSortEngine<T> filterSort,
        //APA AJA YG MESTI DI TRUNUNIN bukan nya harus nya dari virtualdatagridcontrol

        //public UltraCrudPipeline(ColumnCollection columns, Dispatcher uiDispatcher, int channelCapacity = 8192)
        ////public UltraCrudPipeline(HybridDataStore store, FilterSortEngine filterEngine, int channelCapacity = 8192)
        //{
        //    _dataStore = store ?? throw new ArgumentNullException(nameof(store)); ;
        //    var opts = new BoundedChannelOptions(channelCapacity)
        //    {
        //        //UI TETEP RESPONSIF ATAU DATA CPET POTEMSI GANNGGU?
        //        FullMode = BoundedChannelFullMode.Wait,
        //        //FullMode = BoundedChannelFullMode.DropOldest,

        //        SingleReader = true,
        //        SingleWriter = false
        //    };
        //    _channel = Channel.CreateBounded<InternalRow[]>(opts);
        //    //_channel = Channel.CreateBounded<UpdateBatch>(opts);
        //    _filterSortEngine = filterEngine;
        //  //  _bg = new BackgroundProcessor();
        //    _perf = new PerformanceMonitor();

        //    _worker = Task.Run(ProcessBatchesAsync);
        //}

        //public UltraCrudPipeline(ColumnCollection columns, Dispatcher uiDispatcher, int channelCapacity = 8192)
        public UltraCrudPipeline(ColumnCollection columns, int channelCapacity = 8192)
        {
            var options = new BoundedChannelOptions(8192)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<InternalRow[]>(options);
            _dataStore = new HybridDataStore();
            //_filterSortEngine = new FilterSortEngine<T>(columns);
            //    _dispatcher = new ThreadSafeDispatcher(uiDispatcher);
            _perf = new PerformanceMonitor();
            _cts = new CancellationTokenSource();

            _worker = Task.Run(ProcessBatchesAsync);
        }


        #endregion

        /// <summary>
        /// Push collection of entities to pipeline.
        /// This method converts entities externally (caller can use DataConverter) or you can provide InternalRow[] directly.
        /// Overload for convenience: accept InternalRow[] (fast path).
        /// </summary>
        public ValueTask PublishInternalRowsAsync(InternalRow[] rows)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UltraCrudPipeline<T>));
            if (!_channel.Writer.TryWrite(rows))
                return _channel.Writer.WriteAsync(rows);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(InternalRow[] rows, CancellationToken ct = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UltraCrudPipeline<T>));
            if (!_channel.Writer.TryWrite(rows))
                return _channel.Writer.WriteAsync(rows, ct);
            return ValueTask.CompletedTask;
        }


        /// <summary>
        /// Push data yang sudah dikonversi ke InternalRow
        /// Convenience: accept entities and a DataConverter to convert them first (sync).
        /// </summary>
        //public void PushData(IEnumerable<T> data, DataConverter<T> converter)
        //OR
        public ValueTask PushDataAsync(IList<T> items, DataConverter<T> converter)
        {
            if (items == null || items.Count == 0) return ValueTask.CompletedTask;
            var rows = converter.ConvertToInternal(items);
            return PublishInternalRowsAsync(rows);
        }

        /// <summary>
        /// Background loop processes incoming rows and writes into store.
        /// </summary>
        private async Task ProcessBatchesAsync()
        {
            var reader = _channel.Reader;
            try
            {
                while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    var rows = await reader.ReadAsync(_cts.Token).ConfigureAwait(false);
                    using (_perf.Measure(PerfCategory.Data))
                    {
                        // commit each row to store
                        foreach (var r in rows)
                            _dataStore.AddOrUpdate(r);

                        // invalidate cached filtered result
                        lock (_cacheLock)
                        {
                            _filterCacheInvalid = true;
                        }

                        // notify UI via dispatcher (caller should subscribe and handle dispatching to UI thread)
                        DataUpdated?.Invoke(this, new DataUpdatedEventArgs(rows.Length));

                        // optionally report stats
                        StatsUpdated?.Invoke(this, new PipelineStatsEventArgs(GetStats()));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UltraCrudPipeline.ProcessLoopAsync error: {ex}");
            }
        }

        //#region Private Methods - Core Processing
        //private async Task ProcessBatchesAsync()
        //{
        //    try
        //    {
        //        await foreach (var batch in _channel.Reader.ReadAllAsync(_cts.Token))
        //        {
        //            if (_cts.Token.IsCancellationRequested) break;
        //            await ProcessBatchAsync(batch);
        //        }
        //    }
        //    catch (OperationCanceledException) { /* Shutdown */ }
        //}

        //private async Task ProcessBatchAsync(DataBatch<T> batch)
        //{
        //    using (_perfMonitor.MeasureData())
        //    {
        //        try
        //        {
        //            foreach (var row in batch.Data)
        //            {
        //                if (_cts.Token.IsCancellationRequested) break;
        //                _dataStore.AddOrUpdate(row);
        //            }
        //            InvalidateFilterCache();

        //            _dispatcher.InvokeAsync(() =>
        //            {
        //                DataUpdated?.Invoke(this, new DataUpdatedEventArgs(batch.Data.Length));
        //                ReportStats();
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"Batch processing error: {ex.Message}");
        //        }

        //        await Task.Yield();
        //    }
        //}

        //#endregion
        /// <summary>
        /// Get visible data after applying filter/sort.
        /// This method will materialize filtered/sorted array and cache it until next invalidation.
        /// Should be called from background thread or a thread-safe context.
        /// </summary>

        public InternalRow[] GetVisibleData()
        {
            lock (_cacheLock)
            {
                if (!_filterCacheInvalid) return _cachedFiltered;
                var all = _dataStore.SnapshotAll();
                var arr = _filterSortEngine.Apply(all.Span);

                // Release cache lama
                foreach (var r in _cachedFiltered) r.ReleaseHandle();

                // Retain cache baru
                foreach (var r in arr) r.RetainHandle();

                _cachedFiltered = arr;
                _filterCacheInvalid = false;
                return _cachedFiltered;
            }
        }

        /// <summary>
        /// Expose Snapshot (ReadOnlyMemory) for UI renderer; UI must dispatch to UI thread when rendering.
        /// </summary>
        public ReadOnlyMemory<InternalRow> GetVisibleSnapshot(int topIndex, int count)
        {
            var visible = GetVisibleData();
            if (visible == null || visible.Length == 0) return ReadOnlyMemory<InternalRow>.Empty;
            topIndex = Math.Max(0, Math.Min(topIndex, visible.Length));
            var take = Math.Min(count, visible.Length - topIndex);
            if (take <= 0) return ReadOnlyMemory<InternalRow>.Empty;
            var arr = new InternalRow[take];
            Array.Copy(visible, topIndex, arr, 0, take);
            return new ReadOnlyMemory<InternalRow>(arr);
        }

        #region Filter/Sort API (delegates operate on InternalRow)
        public void ApplyGlobalFilter(Func<InternalRow, bool>? predicate)
        {
            _filterSortEngine.ApplyGlobalFilter(predicate);
            InvalidateCacheAndNotify();
        }

        public void SetColumnFilter(int columnIndex, Func<InternalRow, bool> predicate)
        {
            _filterSortEngine.SetColumnFilter(columnIndex, predicate);
            InvalidateCacheAndNotify();
        }

        public void ClearColumnFilter(int columnIndex)
        {
            _filterSortEngine.ClearColumnFilter(columnIndex);
            InvalidateCacheAndNotify();
        }

        public void SetSorts(IEnumerable<(int ColumnIndex, bool Ascending)> sorts)
        {
            _filterSortEngine.SetSorts(sorts);
            InvalidateCacheAndNotify();
        }

        private void InvalidateCacheAndNotify()
        {
            lock (_cacheLock) { _filterCacheInvalid = true; }
            // Fire DataUpdated with zero processed count (signal re-render)
            DataUpdated?.Invoke(this, new DataUpdatedEventArgs(0));
            StatsUpdated?.Invoke(this, new PipelineStatsEventArgs(GetStats()));
        }
        // 🔹 CACHE MANAGEMENT
        //private void InvalidateFilterCache()
        //{
        //    _filterCacheInvalid = true;
        //}

        //private void NotifyDataUpdated()
        //{
        //    _dispatcher.InvokeAsync(() =>
        //    {
        //        DataUpdated?.Invoke(this, new DataUpdatedEventArgs(0));
        //    });
        //}
        #endregion

        public PipelineStats GetStats()
        {
            var total = _dataStore.Count;
            var filtered = (_cachedFiltered?.Length) ?? 0;
            return new PipelineStats
            {
                TotalRows = total,
                FilteredRows = filtered,
                CacheStatus = _filterCacheInvalid ? "Invalid" : "Valid",
                ChannelBacklog = _channel.Reader.Count,
                Performance = new PerformanceStats
                {
                    AverageRenderTime = _perf.GetSnapshot(PerfCategory.Render).Average,
                    AverageDataTime = _perf.GetSnapshot(PerfCategory.Data).Average,
                    FrameRate = _perf.GetSnapshot(PerfCategory.Render).Fps,
                    DataThroughput = _perf.GetSnapshot(PerfCategory.Data).Fps
                }
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _channel.Writer.Complete();
            try { _worker.Wait(500); } catch { }
            foreach (var r in _cachedFiltered) r.ReleaseHandle();

            _dataStore.Dispose();
            //_bg.Dispose();
            _perf.Dispose();
            _cts.Dispose();
        }
    }

    #region Supporting Classes
    public class DataBatch<T>
    {
        public InternalRow[] Data { get; }

        public DataBatch(InternalRow[] data)
        {
            Data = data;
        }
    }
    //atau
    public class DataBatch
    {
        public InternalRow[] Data { get; }

        public DataBatch(InternalRow[] data)
        {
            Data = data;
        }
    }

    public class DataUpdatedEventArgs : EventArgs
    {
        public int ItemsProcessed { get; }
        public DateTime Timestamp { get; }

        public DataUpdatedEventArgs(int itemsProcessed)
        {
            ItemsProcessed = itemsProcessed;
            Timestamp = DateTime.Now;
        }
    }

    public class PipelineStatsEventArgs : EventArgs
    {
        public PipelineStats Stats { get; }

        public PipelineStatsEventArgs(PipelineStats stats)
        {
            Stats = stats;
        }
    }

    public class PipelineStats
    {
        public int TotalRows { get; set; }
        public int FilteredRows { get; set; }
        public string CacheStatus { get; set; }
        public int ChannelBacklog { get; set; }
        public PerformanceStats Performance { get; set; }

        public double FilterRatio => TotalRows > 0 ? (double)FilteredRows / TotalRows : 0;
        public string Summary => $"{FilteredRows:N0} of {TotalRows:N0} rows ({FilterRatio:P1})";
    }
    public class PerformanceStats
    {
        public double AverageRenderTime { get; set; }
        public double AverageDataTime { get; set; }
        public int FrameRate { get; set; }
        public int DataThroughput { get; set; }
    }
    #endregion
}
