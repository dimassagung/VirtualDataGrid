using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// Kategori performance metric untuk grid.
    /// </summary>
    public enum PerfCategory
    {
        Render,
        Data,
        Summary,
        Input,
        Scroll,
        GC // ✅ Monitoring memory & garbage collector
    }

    /// <summary>
    /// Ringkasan hasil metric (avg, min, max, fps, dll).
    /// </summary>
    public readonly struct MetricSnapshot
    {
        public double Average { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public int Count { get; init; }
        public int Fps { get; init; }

        public static MetricSnapshot Empty => new MetricSnapshot { Average = 0, Min = 0, Max = 0, Count = 0, Fps = 0 };

        public override string ToString()
            => $"Avg={Average:F2}ms Min={Min:F2} Max={Max:F2} FPS={Fps}";
    }

    /// <summary>
    /// Monitor performa ringan untuk render/data pipeline/grid state.
    /// </summary>
    public sealed class PerformanceMonitor : IDisposable
    {
        private readonly Dictionary<PerfCategory, Metric> _metrics = new();
        private readonly int _maxSamples;
        private string _gcStats = "";

        public PerformanceMonitor(int maxSamples = 120)
        {
            _maxSamples = maxSamples;
        }

        /// <summary>
        /// Ukur waktu eksekusi block untuk kategori tertentu.
        /// Contoh: using (_perf.Measure(PerfCategory.Render)) { ... }
        /// </summary>
        public IDisposable Measure(PerfCategory category)
            => new Measurement(GetOrCreateMetric(category), _maxSamples);

        /// <summary>
        /// Dapatkan snapshot ringkasan metric untuk kategori tertentu.
        /// </summary>
        public MetricSnapshot GetSnapshot(PerfCategory category)
        {
            if (_metrics.TryGetValue(category, out var metric))
                return metric.GetSnapshot();
            return MetricSnapshot.Empty;
        }
        private Metric GetOrCreateMetric(PerfCategory category)
        {
            if (!_metrics.TryGetValue(category, out var metric))
            {
                metric = new Metric();
                _metrics[category] = metric;
            }
            return metric;
        }

        /// <summary>
        /// Update info GC & memory (panggil tiap beberapa detik via timer).
        /// </summary>
        public void UpdateGC()
        {
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            var mem = GC.GetTotalMemory(false) / 1024.0 / 1024.0; // MB

            var metric = GetOrCreateMetric(PerfCategory.GC);
            metric.AddSample(mem, _maxSamples);

            _gcStats = $"GC0={gen0}, GC1={gen1}, GC2={gen2}, Mem={mem:F1} MB";
        }

        public string GetGcStats() => _gcStats;

        public void Dispose() => _metrics.Clear();

        // ===========================
        // INNER CLASSES
        // ===========================
        private sealed class Measurement : IDisposable
        {
            private readonly Stopwatch _sw;
            private readonly Metric _metric;
            private readonly int _maxSamples;

            public Measurement(Metric metric, int maxSamples)
            {
                _metric = metric;
                _maxSamples = maxSamples;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                _metric.AddSample(_sw.Elapsed.TotalMilliseconds, _maxSamples);
            }
        }

        private sealed class Metric
        {
            private readonly Queue<double> _samples = new();

            public void AddSample(double value, int maxSamples)
            {
                lock (_samples)
                {
                    _samples.Enqueue(value);
                    if (_samples.Count > maxSamples)
                        _samples.Dequeue();
                }
            }

            public MetricSnapshot GetSnapshot()
            {
                lock (_samples)
                {
                    if (_samples.Count == 0) return MetricSnapshot.Empty;

                    var arr = _samples.ToArray();
                    var avg = arr.Average();
                    var fps = avg > 0 ? (int)(1000.0 / avg) : 0;

                    return new MetricSnapshot
                    {
                        Average = avg,
                        Min = arr.Min(),
                        Max = arr.Max(),
                        Count = arr.Length,
                        Fps = fps
                    };
                }
            }
        }
    }
}