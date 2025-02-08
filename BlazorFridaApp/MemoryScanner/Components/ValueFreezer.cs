using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BlazorFridaApp.MemoryScanner.Components
{
    public sealed partial class ValueFreezer : MemoryScannerComponentBase, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<IntPtr, CancellationTokenSource> _freezeOperations = new();
        private readonly SemaphoreSlim _freezeOperationLock = new(1, 1);
        private bool _disposed;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            Logger.LogInformation("ValueFreezer component initialized");
        }

        public bool IsFrozen(IntPtr address)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ValueFreezer));
            return _freezeOperations.ContainsKey(address);
        }

        public async Task ToggleFreeze(IntPtr address, int value)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ValueFreezer));
            
            await _freezeOperationLock.WaitAsync();
            try
            {
                if (_freezeOperations.TryGetValue(address, out var existingCts))
                {
                    // Cancel existing freeze
                    existingCts.Cancel();
                    await Task.Delay(100); // Give time for the operation to stop
                    existingCts.Dispose();
                    _freezeOperations.TryRemove(address, out _);
                    Logger.LogInformation("Unfroze value at address {Address:X}", address);
                }
                else
                {
                    // Start new freeze
                    var cts = new CancellationTokenSource();
                    if (_freezeOperations.TryAdd(address, cts))
                    {
                        _ = StartFreezeOperation(address, value, cts.Token);
                        Logger.LogInformation("Started freezing value at address {Address:X}", address);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling freeze state for address {Address:X}", address);
                throw;
            }
            finally
            {
                _freezeOperationLock.Release();
            }
        }

        private async Task StartFreezeOperation(IntPtr address, int value, CancellationToken cancellationToken)
        {
            try
            {
                using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                var valueBytes = BitConverter.GetBytes(value);

                while (!cancellationToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        await OnValueChanged(address, valueBytes);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Error during freeze operation for address {Address:X}", address);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Freeze operation cancelled for address {Address:X}", address);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Freeze operation failed for address {Address:X}", address);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    // Cancel all freeze operations
                    foreach (var cts in _freezeOperations.Values)
                    {
                        cts.Cancel();
                        await Task.Delay(100); // Give operations time to stop
                        cts.Dispose();
                    }
                    _freezeOperations.Clear();

                    _freezeOperationLock.Dispose();
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error disposing ValueFreezer component");
                }
            }
        }
    }
}