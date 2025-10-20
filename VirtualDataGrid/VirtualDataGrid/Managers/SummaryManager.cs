using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualDataGrid.Managers;

namespace VirtualDataGrid.Managers
{
    /// <summary>
    /// SUMMARY MANAGER - VERSI PRO UNTUK BIG DATA & REALTIME
    /// Fitur:
    /// - Thread-safe dengan immutable collections
    /// - Memory-efficient dengan generic aggregators  
    /// - Cancellation responsive tiap batch
    /// - Progress reporting untuk UI
    /// - Batch processing hindari UI freeze
    /// </summary>
    public sealed class SummaryManager : IDisposable
    {
        private readonly object _sync = new();
        private CancellationTokenSource? _cts;
        private Task? _runningTask;

        // PAKAI IMMUTABLE DICTIONARY - THREAD SAFE
        // Immutable = tidak bisa diubah, jadi aman dari race condition
        private ImmutableDictionary<string, IAggregator> _aggregators =
            ImmutableDictionary<string, IAggregator>.Empty;

        // EVENT UNTUK UI UPDATE
        public event EventHandler<SummaryComputedEventArgs>? SummaryComputed;
        public event EventHandler<SummaryProgressEventArgs>? SummaryProgress;

        public SummaryManager() { }

        /// <summary>
        /// DAFTARKAN AGGREGATOR BARU - VERSI GENERIC (NO BOXING)
        /// </summary>
        public void RegisterAggregator<T>(string key, IAggregator<T> aggregator)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            // ✅ LOCK UNTUK JAMIN THREAD SAFETY  
            lock (_sync)
            {
                // Buat builder dari aggregators yang sekarang
                var builder = _aggregators.ToBuilder();
                // Tambah/timpa aggregator baru
                builder[key] = aggregator;
                // Simpan versi immutable yang baru
                _aggregators = builder.ToImmutable();
            }
        }

        /// <summary>
        /// DAFTARKAN AGGREGATOR CUSTOM - UNTUK LOGIC KOMPLEKS
        /// </summary>
        public void RegisterCustomAggregator(string key, Func<ReadOnlyMemory<dynamic>, object?> aggregatorFunc)
        {
            RegisterAggregator(key, new CustomAggregator(aggregatorFunc));
        }

        /// <summary>
        /// HAPUS AGGREGATOR
        /// </summary>
        public void RemoveAggregator(string key)
        {
            lock (_sync)
            {
                if (_aggregators.ContainsKey(key))
                {
                    var builder = _aggregators.ToBuilder();
                    builder.Remove(key);
                    _aggregators = builder.ToImmutable();
                }
            }
        }

        /// <summary>
        /// HITUNG SUMMARY - MAIN ENTRY POINT
        /// </summary>
        /// <param name="rowsSnapshot">Data yang sudah difilter/processed</param>
        /// <param name="batchSize">Jumlah row per batch (optimasi memory)</param>
        public void ComputeSummary(ReadOnlyMemory<dynamic> rowsSnapshot, int batchSize = 1000)
        {
            lock (_sync)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }

            var token = _cts.Token;
            var aggregators = _aggregators;

            _runningTask = Task.Run(() =>
            {
                try
                {
                    var results = ComputeAggregatorsBatch(aggregators, rowsSnapshot, batchSize, token);

                    if (!token.IsCancellationRequested)
                    {
                        SummaryComputed?.Invoke(this, new SummaryComputedEventArgs(results));
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    SummaryComputed?.Invoke(this,
                        new SummaryComputedEventArgs(new Dictionary<string, object?>
                        {
                            ["__error"] = new AggregateError(ex, "Global computation error")
                        }));
                }
            }, token);
        }

        /// <summary>
        /// PROSES AGGREGATORS DENGAN BATCHING - CORE LOGIC
        /// </summary>
        /// </summary>
        private Dictionary<string, object?> ComputeAggregatorsBatch(
            ImmutableDictionary<string, IAggregator> aggregators,
            ReadOnlyMemory<dynamic> rows,
            int batchSize,
            CancellationToken token)
        {
            var results = new Dictionary<string, object?>();
            int totalRows = rows.Length;

            //LAPORKAN PROGRESS AWAL
            SummaryProgress?.Invoke(this, new SummaryProgressEventArgs(0, totalRows, "Starting..."));

            //PROSES SETIAP AGGREGATOR SATU PER SATU
            foreach (var (key, aggregator) in aggregators)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    object? result = null;
                    int processed = 0;

                    // FIX: PROSES PER BATCH DARI MEMORY
                    for (int start = 0; start < totalRows; start += batchSize)
                    {
                        token.ThrowIfCancellationRequested();

                        int end = Math.Min(start + batchSize, totalRows);
                        var batch = rows.Slice(start, end - start);

                        result = aggregator.ProcessBatch(batch, result, token);
                        processed += batch.Length;

                        SummaryProgress?.Invoke(this,
                            new SummaryProgressEventArgs(processed, totalRows, key));
                    }

                    results[key] = result;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    results[key] = new AggregateError(ex, key);
                }
            }

            //LAPORKAN PROGRESS SELESAI
            SummaryProgress?.Invoke(this, new SummaryProgressEventArgs(totalRows, totalRows, "Completed"));

            return results;
        }

        /// <summary>
        /// BATALKAN COMPUTATION YANG SEDANG BERJALAN
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
        }
    }


    // ==================== INTERFACES & BASE CLASSES ====================

    public interface IAggregator
    {
        object? ProcessBatch(ReadOnlyMemory<dynamic> batch, object? previousState, CancellationToken token);
    }

    public interface IAggregator<T> : IAggregator
    {
        T ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, T previousState, CancellationToken token);
    }

    /// <summary>
    /// BASE CLASS - FIX: TIDAK PAKAI NULLABLE UNTUK STATE
    /// </summary>
    public abstract class AggregatorBase<T> : IAggregator<T>
    {
        object? IAggregator.ProcessBatch(ReadOnlyMemory<dynamic> batch, object? previousState, CancellationToken token)
        {
            // Convert null ke default value untuk type T
            T state = previousState != null ? (T)previousState : default(T)!;
            return ProcessBatchTyped(batch, state, token);
        }

        public abstract T ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, T previousState, CancellationToken token);
    }

    // ==================== BUILT-IN AGGREGATORS ====================

    /// <summary>
    /// AGGREGATOR UNTUK SUM - FIX: TIDAK PAKAI NULLABLE
    /// </summary>
    public class SumAggregator : AggregatorBase<double>
    {
        private readonly int _columnIndex;

        public SumAggregator(int columnIndex) => _columnIndex = columnIndex;

        public override double ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, double previousState, CancellationToken token)
        {
            double sum = previousState;
            var span = batch.Span; // Convert Memory ke Span untuk performance

            for (int i = 0; i < span.Length; i++)
            {
                token.ThrowIfCancellationRequested();

                var cellValue = span[i].GetValue(_columnIndex);
                if (cellValue.IsNumeric)
                    sum += cellValue.NumericValue;
            }

            return sum;
        }
    }

    /// <summary>
    /// AGGREGATOR UNTUK COUNT - FIX: TIDAK PAKAI NULLABLE
    /// </summary>
    public class CountAggregator : AggregatorBase<int>
    {
        public override int ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, int previousState, CancellationToken token)
        {
            return previousState + batch.Length;
        }
    }

    /// <summary>
    /// AGGREGATOR UNTUK AVERAGE - FIX: STATE YANG BENAR
    /// </summary>
    public class AverageAggregator : AggregatorBase<AverageState>
    {
        private readonly int _columnIndex;

        public AverageAggregator(int columnIndex) => _columnIndex = columnIndex;

        public override AverageState ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, AverageState previousState, CancellationToken token)
        {
            var state = previousState;
            var span = batch.Span;

            for (int i = 0; i < span.Length; i++)
            {
                token.ThrowIfCancellationRequested();

                var cellValue = span[i].GetValue(_columnIndex);
                if (cellValue.IsNumeric)
                {
                    state.Sum += cellValue.NumericValue;
                    state.Count++;
                }
            }

            return state;
        }
    }

    /// <summary>
    /// STATE UNTUK AVERAGE CALCULATION
    /// </summary>
    public struct AverageState
    {
        public double Sum { get; set; }
        public int Count { get; set; }

        public double Average => Count > 0 ? Sum / Count : 0.0;

        public override string ToString() => Average.ToString("F2");
    }

    /// <summary>
    /// AGGREGATOR UNTUK MIN/MAX
    /// </summary>
    public class MinMaxAggregator : AggregatorBase<MinMaxState>
    {
        private readonly int _columnIndex;

        public MinMaxAggregator(int columnIndex) => _columnIndex = columnIndex;

        public override MinMaxState ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, MinMaxState previousState, CancellationToken token)
        {
            var state = previousState;
            var span = batch.Span;

            for (int i = 0; i < span.Length; i++)
            {
                token.ThrowIfCancellationRequested();

                var cellValue = span[i].GetValue(_columnIndex);
                if (cellValue.IsNumeric)
                {
                    var value = cellValue.NumericValue;
                    if (value < state.Min) state.Min = value;
                    if (value > state.Max) state.Max = value;
                    state.HasValue = true;
                }
            }

            return state;
        }
    }
    /// <summary>
    /// STATE UNTUK MIN/MAX CALCULATION
    /// </summary>
    public struct MinMaxState
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public bool HasValue { get; set; }

        public override string ToString() => HasValue ? $"{Min} / {Max}" : "N/A";
    }

    /// <summary>
    /// AGGREGATOR CUSTOM - FIX: PAKAI READONLYMEMORY
    /// </summary>
    public class CustomAggregator : AggregatorBase<object>
    {
        private readonly Func<ReadOnlyMemory<dynamic>, object?> _aggregatorFunc;

        public CustomAggregator(Func<ReadOnlyMemory<dynamic>, object?> aggregatorFunc)
            => _aggregatorFunc = aggregatorFunc;

        public override object ProcessBatchTyped(ReadOnlyMemory<dynamic> batch, object previousState, CancellationToken token)
        {
            // Untuk custom, kita ignore batching dan jalankan semua sekaligus
            return _aggregatorFunc(batch) ?? "N/A";
        }
    }

    // ==================== EVENT CLASSES ====================

    /// <summary>
    /// EVENT UNTUK KIRIM HASIL SUMMARY KE UI
    /// </summary>
    public class SummaryComputedEventArgs : EventArgs
    {
        public IReadOnlyDictionary<string, object?> Results { get; }

        public SummaryComputedEventArgs(IReadOnlyDictionary<string, object?> results)
            => Results = results;
    }

    /// <summary>
    /// EVENT UNTUK LAPORAN PROGRESS
    /// </summary>
    public class SummaryProgressEventArgs : EventArgs
    {
        public int ProcessedRows { get; }
        public int TotalRows { get; }
        public string? CurrentAggregator { get; }

        public double ProgressPercentage => TotalRows > 0 ? (ProcessedRows * 100.0) / TotalRows : 0;

        public SummaryProgressEventArgs(int processed, int total, string? current = null)
            => (ProcessedRows, TotalRows, CurrentAggregator) = (processed, total, current);
    }

    /// <summary>
    /// CLASS UNTUK SIMPAN INFO ERROR
    /// </summary>

    public class AggregateError
    {
        public Exception Exception { get; }
        public string AggregatorKey { get; }
        public DateTime ErrorTime { get; }

        public AggregateError(Exception ex, string key)
            => (Exception, AggregatorKey, ErrorTime) = (ex, key, DateTime.Now);

        public override string ToString()
            => $"[{ErrorTime:HH:mm:ss}] Error in '{AggregatorKey}': {Exception.Message}";
    }
}


////CARA PAKE
//// ✅ SETUP SUMMARY MANAGER
//var summaryManager = new SummaryManager();

//// ✅ DAFTARKAN AGGREGATORS STANDARD
//summaryManager.RegisterAggregator("TotalVolume", new SumAggregator(columnIndex: 2));
//summaryManager.RegisterAggregator("RowCount", new CountAggregator());
//summaryManager.RegisterAggregator("AveragePrice", new AverageAggregator(columnIndex: 3));
//summaryManager.RegisterAggregator("PriceRange", new MinMaxAggregator(columnIndex: 3));

//// ✅ DAFTARKAN CUSTOM AGGREGATOR - FIX: PAKAI READONLYMEMORY<dynamic>
//summaryManager.RegisterCustomAggregator("ProfitMargin", (ReadOnlyMemory<dynamic> rows) =>
//{
//    double totalProfit = 0, totalRevenue = 0;
//    var span = rows.Span;

//    for (int i = 0; i < span.Length; i++)
//    {
//        var row = span[i];
//        var profit = row.GetValue(4).NumericValue;   // Column 4 = Profit
//        var revenue = row.GetValue(5).NumericValue;  // Column 5 = Revenue
//        totalProfit += profit;
//        totalRevenue += revenue;
//    }

//    return totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;
//});

//// ✅ EVENT HANDLERS
//summaryManager.SummaryComputed += (sender, e) =>
//{
//    Console.WriteLine("=== SUMMARY RESULTS ===");
//    foreach (var result in e.Results)
//    {
//        if (result.Value is AggregateError error)
//        {
//            Console.WriteLine($"❌ {result.Key}: {error}");
//        }
//        else
//        {
//            Console.WriteLine($"✅ {result.Key}: {result.Value}");
//        }
//    }
//};

//summaryManager.SummaryProgress += (sender, e) =>
//{
//    Console.WriteLine($"🔄 {e.CurrentAggregator}: {e.ProcessedRows}/{e.TotalRows} ({e.ProgressPercentage:F1}%)");
//};

//// ✅ JALANKAN COMPUTATION - FIX: PAKAI READONLYMEMORY<dynamic>
//ReadOnlyMemory<dynamic> filteredData = GetFilteredData();
//summaryManager.ComputeSummary(filteredData, batchSize: 5000);

//// ✅ TUNGGU SELESAI (optional)
//await Task.Delay(1000); // Atau tunggu event SummaryComputed

//// ✅ CLEANUP SAAT TUTUP APLIKASI
//// summaryManager.Dispose();