using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

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
                        // Add detach logic here if needed
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
            finally
            {
                GC.SuppressFinalize(this);
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

    public interface IMemoryCleanupService
    {
        Task CleanupAsync();
    }

    public class MemoryCleanupService : IMemoryCleanupService
    {
        private readonly BlazorFridaApp.MemoryScanner.Services.Interfaces.IFridaInteropService _fridaInterop;
        private readonly BlazorFridaApp.MemoryScanner.Services.Interfaces.IPythonRuntimeService _pythonRuntime;
        private readonly ILogger<MemoryCleanupService> _logger;

        public MemoryCleanupService(
            BlazorFridaApp.MemoryScanner.Services.Interfaces.IFridaInteropService fridaInterop,
            BlazorFridaApp.MemoryScanner.Services.Interfaces.IPythonRuntimeService pythonRuntime,
            ILogger<MemoryCleanupService> logger)
        {
            _fridaInterop = fridaInterop;
            _pythonRuntime = pythonRuntime;
            _logger = logger;
        }

        public async Task CleanupAsync()
        {
            try
            {
                await _fridaInterop.DetachAsync();
                _pythonRuntime.ReleaseGIL();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory cleanup");
            }
        }
    }
}