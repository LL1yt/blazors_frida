using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase, IAsyncDisposable
    {
        [Inject] private MemoryScannerService ScannerService { get; set; } = default!;
        [Inject] new private INotificationService NotificationService { get; set; } = default!;

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
                
                // Log initial state
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

        protected override async Task OnParametersSetAsync()
        {
            Logger.LogDebug("Parameters being set for MemoryScanner component after {ElapsedMs}ms", 
                _componentLifetimeStopwatch.ElapsedMilliseconds);
            await base.OnParametersSetAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Logger.LogInformation(
                    "MemoryScanner component rendered for the first time after {ElapsedMs}ms. Component ID: {ComponentId}",
                    _componentLifetimeStopwatch.ElapsedMilliseconds, GetHashCode());
                
                try
                {
                    await RefreshProcessList();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to perform initial process list refresh");
                }
            }
            else
            {
                Logger.LogTrace(
                    "MemoryScanner component re-rendered after {ElapsedMs}ms. State: {{ ProcessId: {ProcessId}, IsLoading: {IsLoading} }}",
                    _componentLifetimeStopwatch.ElapsedMilliseconds, _state.SelectedProcessId, _state.IsLoading);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Logger.LogInformation(
                "Disposing MemoryScanner component after {ElapsedMs}ms. Component ID: {ComponentId}",
                _componentLifetimeStopwatch.ElapsedMilliseconds, GetHashCode());
            
            try
            {
                if (ScannerService is IAsyncDisposable disposableService)
                {
                    Logger.LogDebug("Disposing scanner service");
                    await disposableService.DisposeAsync();
                }
                
                if (scanExecutor is IAsyncDisposable disposableExecutor)
                {
                    Logger.LogDebug("Disposing scan executor");
                    await disposableExecutor.DisposeAsync();
                }
                
                if (valueHandler is IAsyncDisposable disposableHandler)
                {
                    Logger.LogDebug("Disposing value handler");
                    await disposableHandler.DisposeAsync();
                }
                
                if (valueFreezer is IAsyncDisposable disposableFreezer)
                {
                    Logger.LogDebug("Disposing value freezer");
                    await disposableFreezer.DisposeAsync();
                    valueFreezer = null!;
                }

                _componentLifetimeStopwatch.Stop();
                Logger.LogInformation("MemoryScanner component disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during MemoryScanner component disposal");
                throw;
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}