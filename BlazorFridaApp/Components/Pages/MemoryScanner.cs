using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase, IAsyncDisposable
    {
        [Inject] private MemoryScannerService ScannerService { get; set; } = default!;
        [Inject] new private INotificationService NotificationService { get; set; } = default!;

        private ScanExecutor scanExecutor = default!;
        private MemoryValueHandler valueHandler = default!;
        private ValueFreezer valueFreezer = default!;

        public async ValueTask DisposeAsync()
        {
            if (ScannerService is IAsyncDisposable disposableService)
            {
                await disposableService.DisposeAsync();
            }
            
            // Dispose other resources if needed
            if (scanExecutor is IAsyncDisposable disposableExecutor)
            {
                await disposableExecutor.DisposeAsync();
            }
            
            if (valueHandler is IAsyncDisposable disposableHandler)
            {
                await disposableHandler.DisposeAsync();
            }
            
            if (valueFreezer is IAsyncDisposable disposableFreezer)
            {
                await disposableFreezer.DisposeAsync();
                valueFreezer = null!;
            }

            GC.SuppressFinalize(this);
        }
    }
}