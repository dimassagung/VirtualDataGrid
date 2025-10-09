using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using VirtualDataGrid.Core;

namespace VirtualDataGrid.Data
{
    /// <summary>
    /// High-performance background data processor dengan generic support
    /// Handle data conversion, filtering, dan sorting di background thread
    /// </summary>
    public sealed class BackgroundProcessor<T> : IDisposable where T : class
    {
        #region Private Fields
        private readonly Channel<ProcessingRequest<T>> _channel;
        private readonly DataConverter<T> _dataConverter;
        private readonly UltraCrudPipeline<T> _pipeline;
        private readonly CancellationTokenSource _cts;
        private Task _processingTask;
        private bool _disposed = false;
        private long _totalProcessed = 0;
        #endregion

        #region Events
        public event EventHandler<ProcessingCompletedEventArgs<T>> ProcessingCompleted;
        public event EventHandler<ProcessingErrorEventArgs> ProcessingError;
        #endregion

        #region Constructor
        public BackgroundProcessor(ColumnCollection columns, UltraCrudPipeline<T> pipeline)
        {
            // Channel untuk async processing
            var channelOptions = new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<ProcessingRequest<T>>(channelOptions);

            // Core components
            _dataConverter = new DataConverter<T>(columns);
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _cts = new CancellationTokenSource();

            // Start background processing
            _processingTask = Task.Run(ProcessRequestsAsync);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Process data di background thread (non-blocking)
        /// </summary>
        public async Task ProcessDataAsync(IEnumerable<T> data, ProcessingPriority priority = ProcessingPriority.Normal)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BackgroundProcessor<T>));

            var request = new ProcessingRequest<T>
            {
                Data = data,
                Priority = priority,
                RequestId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            };

            await _channel.Writer.WriteAsync(request, _cts.Token);
        }

        /// <summary>
        /// Get processing statistics
        /// </summary>
        public ProcessorStats GetStats()
        {
            return new ProcessorStats
            {
                TotalProcessed = Interlocked.Read(ref _totalProcessed),
                ChannelBacklog = _channel.Reader.Count,
                IsProcessing = _processingTask?.Status == TaskStatus.Running
            };
        }

        /// <summary>
        /// Wait for all pending processing to complete
        /// </summary>
        public async Task WaitForCompletionAsync(TimeSpan timeout)
        {
            if (_channel.Reader.Count == 0) return;

            using var timeoutCts = new CancellationTokenSource(timeout);
            try
            {
                while (_channel.Reader.Count > 0 && !timeoutCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(10, timeoutCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout reached
            }
        }
        #endregion

        #region Private Methods - Core Processing
        private async Task ProcessRequestsAsync()
        {
            try
            {
                await foreach (var request in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    if (_cts.Token.IsCancellationRequested) break;
                    await ProcessRequestAsync(request);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(ex));
            }
        }

        private async Task ProcessRequestAsync(ProcessingRequest<T> request)
        {
            try
            {
                //// STEP 1: Convert to InternalRow (dengan object pooling)
                //var internalData = _dataConverter.ConvertToInternal(request.Data);

                //// STEP 2: Push to pipeline untuk filtering/sorting
                //_pipeline.PushData(request.Data, _dataConverter);

                //// STEP 3: Update statistics
                //Interlocked.Add(ref _totalProcessed, internalData.Length);

                //// STEP 4: Notify completion
                //var result = new ProcessingResult<T>
                //{
                //    RequestId = request.RequestId,
                //    ProcessedRows = internalData.Length,
                //    Timestamp = DateTime.UtcNow,
                //    OriginalData = request.Data
                //};

                //ProcessingCompleted?.Invoke(this, new ProcessingCompletedEventArgs<T>(result));

                // STEP 5: Yield untuk prevent blocking
                await Task.Yield();
            }
            catch (Exception ex)
            {
                ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(ex, request.RequestId));
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cts.Cancel();
            _channel.Writer.Complete();

            try
            {
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { /* Expected */ }

            // CRITICAL: Dispose DataConverter untuk cleanup object pools
            _dataConverter?.Dispose();
            _cts?.Dispose();
        }
        #endregion
    }

    #region Supporting Classes
    public class ProcessingRequest<T>
    {
        public IEnumerable<T> Data { get; set; }
        public ProcessingPriority Priority { get; set; }
        public Guid RequestId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ProcessingResult<T>
    {
        public Guid RequestId { get; set; }
        public int ProcessedRows { get; set; }
        public DateTime Timestamp { get; set; }
        public IEnumerable<T> OriginalData { get; set; }
    }

    public class ProcessingCompletedEventArgs<T> : EventArgs
    {
        public ProcessingResult<T> Result { get; }

        public ProcessingCompletedEventArgs(ProcessingResult<T> result)
        {
            Result = result;
        }
    }

    public class ProcessingErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public Guid? RequestId { get; }

        public ProcessingErrorEventArgs(Exception exception, Guid? requestId = null)
        {
            Exception = exception;
            RequestId = requestId;
        }
    }

    public enum ProcessingPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public class ProcessorStats
    {
        public long TotalProcessed { get; set; }
        public int ChannelBacklog { get; set; }
        public bool IsProcessing { get; set; }

        public override string ToString() => $"Processed: {TotalProcessed:N0}, Backlog: {ChannelBacklog}, Active: {IsProcessing}";
    }
    #endregion
}