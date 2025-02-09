using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Models;
using System.Runtime.InteropServices;
using System;
using BlazorFridaApp.Services.Interfaces;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase
    {
        [Inject] protected new MemoryScannerService ScannerService { get; set; } = default!;
        [Inject] protected new INotificationService NotificationService { get; set; } = default!;
        [Inject] protected IMemoryCleanupService CleanupService { get; set; } = default!;

        private readonly Stopwatch _componentLifetimeStopwatch = new();
        private ScanExecutor scanExecutor = default!;
        private MemoryValueHandler valueHandler = default!;
        private ValueFreezer valueFreezer = default!;

        protected override async Task OnInitializedAsync()
        {
            _componentLifetimeStopwatch.Start();
            Logger.LogInformation("Initializing MemoryScanner component. Component ID: {ComponentId}", GetHashCode());
            
            try
            {
                await base.OnInitializedAsync();
                Logger.LogInformation("Base initialization completed after {ElapsedMs}ms", _componentLifetimeStopwatch.ElapsedMilliseconds);
                
                Logger.LogDebug("Initial state - Selected Process: {ProcessId}, Scan Type: {ScanType}, Value Type: {ValueType}",
                    _state.SelectedProcessId, _state.SelectedScanType, _state.SelectedValueType);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize MemoryScanner component after {ElapsedMs}ms", 
                    _componentLifetimeStopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        public override async Task RefreshProcessList()
        {
            Logger.LogInformation("Starting process list refresh");
            _state.IsLoading = true;
            StateHasChanged();
            
            try
            {
                // Clean up existing resources before refresh
                if (_state?.SelectedProcessId.HasValue == true)
                {
                    Logger.LogDebug("Cleaning up before process list refresh. Current ProcessId: {ProcessId}", _state.SelectedProcessId.Value);
                    try
                    {
                        await CleanupService.CleanupAsync();
                        Logger.LogDebug("Cleanup completed successfully");
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.LogWarning(cleanupEx, "Non-critical error during cleanup");
                    }
                }

                Logger.LogDebug("Starting process list refresh. Current state: {@State}", _state);
                ArgumentNullException.ThrowIfNull(_state, nameof(_state));

                var sw = Stopwatch.StartNew();
                Logger.LogDebug("Calling ScannerService.RefreshProcessList");
                await ScannerService.RefreshProcessList(_state);
                sw.Stop();

                Logger.LogInformation(
                    "Process list refreshed successfully in {ElapsedMs}ms. Found {Count} processes. State after refresh: {@State}", 
                    sw.ElapsedMilliseconds,
                    _state.ProcessList?.Count ?? 0,
                    _state);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh process list. Last known state: {@State}", _state);
                NotificationService.ShowError("Process List Error", 
                    $"Failed to refresh process list: {ex.Message}. Check if you have the necessary permissions.");
                throw;
            }
            finally
            {
                _state.IsLoading = false;
                Logger.LogDebug("Process list refresh completed. Final state: {@State}", _state);
                StateHasChanged();
            }
        }

        public override async Task OnProcessSelected()
        {
            try
            {
                Logger.LogInformation("Process selected: ID {ProcessId}", _state.SelectedProcessId);
                await ScannerService.OnProcessSelected(_state);
                Logger.LogDebug("Process selection handled successfully");
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling process selection for ID {ProcessId}", _state.SelectedProcessId);
                throw;
            }
        }

        public override async Task OnScanTypeChanged(ScanType newType)
        {
            try
            {
                Logger.LogInformation("Scan type changing from {OldType} to {NewType}",
                    _state.SelectedScanType, newType);
                await ScannerService.OnScanTypeChanged(_state, newType);
                Logger.LogDebug("Scan type changed successfully");
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error changing scan type to {NewType}", newType);
                throw;
            }
        }

        public override void OnValueTypeChanged(MemoryValueType newType)
        {
            try
            {
                Logger.LogInformation("Value type changing from {OldType} to {NewType}",
                    _state.SelectedValueType, newType);
                ScannerService.OnValueTypeChanged(_state, newType);
                Logger.LogDebug("Value type changed successfully");
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error changing value type to {NewType}", newType);
                throw;
            }
        }

        public override async Task OnScan()
        {
            if (!_state.CanScan)
            {
                Logger.LogWarning("Scan attempted but CanScan is false");
                return;
            }

            if (!_state.SelectedProcessId.HasValue)
            {
                Logger.LogWarning("Scan attempted without selected process");
                NotificationService.ShowError("Scan Error", "Please select a process first");
                return;
            }

            if (scanExecutor == null)
            {
                Logger.LogError("ScanExecutor is null");
                NotificationService.ShowError("Scan Error", "Scanner component not initialized properly");
                return;
            }

            try
            {
                Logger.LogInformation("Starting memory scan with parameters - ProcessId: {ProcessId}, ScanType: {ScanType}, ValueType: {ValueType}, IsFirstScan: {IsFirstScan}",
                    _state.SelectedProcessId, _state.SelectedScanType, _state.SelectedValueType, _state.IsFirstScan);
                
                _state.IsLoading = true;
                StateHasChanged();

                Logger.LogDebug("Starting background scan task");
                await Task.Run(async () =>
                {
                    Logger.LogDebug("Background task started");
                    try 
                    {
                        Logger.LogDebug("Executing scan with executor: {@ScanExecutor}", scanExecutor);
                        await scanExecutor.ExecuteScan(addr => {
                            try
                            {
                                Logger.LogTrace("Reading value at address: {Address:X}", addr);
                                var value = GetCurrentValue(addr).Result;
                                if (value == null || value.Length == 0)
                                {
                                    Logger.LogWarning("Got empty value at address {Address:X}", addr);
                                    return 0;
                                }
                                var result = BitConverter.ToInt32(value, 0);
                                Logger.LogTrace("Value read at {Address:X}: {Value}", addr, result);
                                return result;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Error reading value at {Address:X}", addr);
                                return 0;
                            }
                        });
                        Logger.LogDebug("Scan execution completed in background task");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error in background scan task. State: {@State}", _state);
                        throw;
                    }
                });

                Logger.LogInformation("Scan execution completed successfully. State: IsLoading={IsLoading}", _state.IsLoading);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing memory scan. State: {@State}", _state);
                NotificationService.ShowError("Scan Error", $"Error during scan: {ex.Message}");
            }
            finally
            {
                _state.IsLoading = false;
                Logger.LogDebug("Scan cleanup completed. Final state: {@State}", _state);
                StateHasChanged();
            }
        }

        public override void OnScanComplete(List<nint> results)
        {
            try
            {
                Logger.LogInformation("Scan completed with {Count} results", results.Count);
                _state.OnScanComplete(results);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling scan completion");
                throw;
            }
        }

        public override void OnLoadingChanged(bool isLoading)
        {
            _state.IsLoading = isLoading;
            StateHasChanged();
        }

        public override async Task SaveConfig()
        {
            try
            {
                Logger.LogInformation("Saving scan configuration");
                // TODO: Implement configuration saving
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving scan configuration");
                throw;
            }
        }

        public override async Task LoadConfig()
        {
            try
            {
                Logger.LogInformation("Loading scan configuration");
                // TODO: Implement configuration loading
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading scan configuration");
                throw;
            }
        }

        public override Task<byte[]> GetCurrentValue(nint address)
        {
            try
            {
                return valueHandler.GetCurrentValue((IntPtr)address);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting current value at address {Address:X}", address);
                throw;
            }
        }

        public override bool IsFrozen(nint address)
        {
            try
            {
                return valueFreezer.IsFrozen((IntPtr)address);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking freeze state for address {Address:X}", address);
                throw;
            }
        }

        public override Task OnValueChanged(nint address, byte[] newValue)
        {
            try
            {
                return valueHandler.OnValueChanged(((IntPtr)address, newValue));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling value change at address {Address:X}", address);
                throw;
            }
        }

        private byte[] GetDefaultValueBytes(MemoryValueType valueType)
        {
            try
            {
                return valueType switch
                {
                    MemoryValueType.Byte => new byte[] { 0 },
                    MemoryValueType.Int16 => BitConverter.GetBytes((short)0),
                    MemoryValueType.Int32 => BitConverter.GetBytes(0),
                    MemoryValueType.Int64 => BitConverter.GetBytes((long)0),
                    MemoryValueType.Float => BitConverter.GetBytes(0.0f),
                    MemoryValueType.Double => BitConverter.GetBytes(0.0),
                    _ => BitConverter.GetBytes(0)
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting default value bytes for type {ValueType}", valueType);
                throw;
            }
        }

        public override Task ToggleFreeze(nint address, byte[] value)
        {
            try
            {
                var intValue = _state.SelectedValueType switch
                {
                    MemoryValueType.Byte => (int)value[0],
                    MemoryValueType.Int16 => (int)BitConverter.ToInt16(value, 0),
                    MemoryValueType.Int32 => BitConverter.ToInt32(value, 0),
                    MemoryValueType.Int64 => (int)BitConverter.ToInt64(value, 0),
                    MemoryValueType.Float => (int)BitConverter.ToSingle(value, 0),
                    MemoryValueType.Double => (int)BitConverter.ToDouble(value, 0),
                    _ => BitConverter.ToInt32(value, 0)
                };
                return valueFreezer.ToggleFreeze((IntPtr)address, intValue);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling freeze state for address {Address:X}", address);
                throw;
            }
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            Logger.LogInformation(
                "Disposing MemoryScanner component after {ElapsedMs}ms. Component ID: {ComponentId}",
                _componentLifetimeStopwatch.ElapsedMilliseconds, GetHashCode());
            
            try
            {
                // First, stop any ongoing scans
                if (scanExecutor != null)
                {
                    Logger.LogDebug("Stopping any ongoing scans");
                    scanExecutor.Reset();
                }

                // Release Frida resources first
                if (ScannerService is IAsyncDisposable disposableService)
                {
                    Logger.LogDebug("Disposing scanner service");
                    await disposableService.DisposeAsync().ConfigureAwait(false);
                }
                

                // Ensure Python runtime is released
                if (_state?.SelectedProcessId.HasValue == true)
                {
                    Logger.LogDebug("Detaching from process {ProcessId}", _state.SelectedProcessId.Value);
                    try
                    {
                        await CleanupService.CleanupAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Error detaching from process");
                    }
                }

                // Dispose other components
                await DisposeManagedResources().ConfigureAwait(false);
                
                _componentLifetimeStopwatch.Stop();
                Logger.LogInformation("MemoryScanner component disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during MemoryScanner component disposal");
                throw;
            }
        }

        private async Task DisposeManagedResources()
        {
            if (scanExecutor is IAsyncDisposable disposableExecutor)
            {
                await disposableExecutor.DisposeAsync().ConfigureAwait(false);
            }

            if (valueHandler is IAsyncDisposable disposableHandler)
            {
                await disposableHandler.DisposeAsync().ConfigureAwait(false);
            }

            if (valueFreezer is IAsyncDisposable disposableFreezer)
            {
                await disposableFreezer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
