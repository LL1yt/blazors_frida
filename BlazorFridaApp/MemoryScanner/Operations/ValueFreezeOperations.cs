using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public interface IValueFreezeOperations : IAsyncDisposable
    {
        Task FreezeValue(nint address, byte[] value, string valueType);
        Task UnfreezeValue(nint address);
        Task UnfreezeAll(int processId);
    }

    public sealed class ValueFreezeOperations : MemoryScannerBase, IValueFreezeOperations
    {
        private readonly IValueFreezerService _freezer;
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<nint, CancellationTokenSource>> _activeFreezeTasks;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;
        private const int MaxConcurrentFreezes = 16;

        public ValueFreezeOperations(
            IValueFreezerService freezer,
            ILogger<ValueFreezeOperations> logger) : base(logger)
        {
            _freezer = freezer;
            _activeFreezeTasks = new ConcurrentDictionary<int, ConcurrentDictionary<nint, CancellationTokenSource>>();
            _semaphore = new SemaphoreSlim(MaxConcurrentFreezes, MaxConcurrentFreezes);
        }

        public async Task UnfreezeAll(int processId)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ValueFreezeOperations));

            if (_activeFreezeTasks.TryGetValue(processId, out var processFreezes))
            {
                var tasks = new List<Task>();
                foreach (var address in processFreezes.Keys)
                {
                    tasks.Add(UnfreezeValue(address));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                _activeFreezeTasks.TryRemove(processId, out _);
            }
        }

        public async Task FreezeValue(nint address, byte[] value, string valueType)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ValueFreezeOperations));

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var processId = GetProcessIdFromAddress(address);
                var processFreezes = _activeFreezeTasks.GetOrAdd(processId, 
                    _ => new ConcurrentDictionary<nint, CancellationTokenSource>());

                // Cancel any existing freeze operation for this address
                if (processFreezes.TryGetValue(address, out var existingCts))
                {
                    await UnfreezeValue(address).ConfigureAwait(false);
                }

                var cts = new CancellationTokenSource();
                if (processFreezes.TryAdd(address, cts))
                {
                    await ExecuteWithLogging(
                        async () =>
                        {
                            try
                            {
                                await _freezer.FreezeValue(address, value, valueType);
                            }
                            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                            {
                                _logger.LogInformation("Freeze operation cancelled for address {Address:X}", address);
                            }
                        },
                        "Freezing value",
                        address, valueType);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UnfreezeValue(nint address)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ValueFreezeOperations));

            var processId = GetProcessIdFromAddress(address);
            if (_activeFreezeTasks.TryGetValue(processId, out var processFreezes) &&
                processFreezes.TryRemove(address, out var cts))
            {
                await ExecuteWithLogging(
                    async () =>
                    {
                        try
                        {
                            cts.Cancel();
                            await _freezer.UnfreezeValue(address);
                        }
                        finally
                        {
                            cts.Dispose();
                        }
                    },
                    "Unfreezing value",
                    address);

                if (processFreezes.IsEmpty)
                {
                    _activeFreezeTasks.TryRemove(processId, out _);
                }
            }
        }

        private static int GetProcessIdFromAddress(nint address)
        {
            // Extract process ID from the high bits of the address
            // This assumes the address format includes process ID information
            return (int)((ulong)address >> 48);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await DisposeAsyncCore().ConfigureAwait(false);
                Dispose(disposing: false);
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private async ValueTask DisposeAsyncCore()
        {
            try
            {
                // Cancel and cleanup all active freeze operations
                foreach (var processFreezes in _activeFreezeTasks.Values)
                {
                    foreach (var (address, cts) in processFreezes)
                    {
                        await UnfreezeValue(address).ConfigureAwait(false);
                    }
                    processFreezes.Clear();
                }
                _activeFreezeTasks.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up freeze operations");
            }
            finally
            {
                _semaphore.Dispose();
            }
        }

        protected override void OnDispose()
        {
            foreach (var processFreezes in _activeFreezeTasks.Values)
            {
                foreach (var cts in processFreezes.Values)
                {
                    cts.Dispose();
                }
                processFreezes.Clear();
            }
            _activeFreezeTasks.Clear();
            _semaphore.Dispose();
        }
    }
}