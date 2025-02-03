 using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Models;
using System.Runtime.InteropServices;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase
    {
        [Inject] protected new MemoryScannerService ScannerService { get; set; } = default!;
        [Inject] protected new INotificationService NotificationService { get; set; } = default!;
        [Inject] protected new ILogger<MemoryScanner> Logger { get; set; } = default!;

        private readonly Stopwatch _componentLifetimeStopwatch = new();
        private ScanExecutor scanExecutor = default!;
        private MemoryValueHandler valueHandler = default!;
        private ValueFreezer valueFreezer = default!;
        protected MemoryScannerState _state => base._state;

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
                Logger.LogDebug("Calling ScannerService.RefreshProcessList");
                await ScannerService.RefreshProcessList(_state);
                Logger.LogInformation("Process list refreshed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh process list");
                throw;
            }
            finally
            {
                _state.IsLoading = false;
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

            try
            {
                Logger.LogInformation("Starting memory scan");
                await scanExecutor.ExecuteScan(GetCurrentValue);
                Logger.LogDebug("Scan execution initiated successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing memory scan");
                throw;
            }
        }

        public override void OnScanComplete(List<IntPtr> results)
        {
            try
            {
                Logger.LogInformation("Scan completed with {Count} results", results.Count);
                _state.OnScanComplete(results.ConvertAll(ptr => (nint)ptr));
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

        public override async Task<byte[]> GetCurrentValue(nint address)
        {
            try
            {
                return await valueHandler.GetCurrentValue((IntPtr)address);
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

        public override async Task OnValueChanged(nint address, byte[] newValue)
        {
            try
            {
                await valueHandler.OnValueChanged((IntPtr)address, newValue);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling value change at address {Address:X}", address);
                throw;
            }
        }

        public override async Task ToggleFreeze(nint address, byte[] value)
        {
            try
            {
                await valueFreezer.ToggleFreeze((IntPtr)address, value);
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