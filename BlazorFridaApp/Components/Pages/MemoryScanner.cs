using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase, IAsyncDisposable
    {
        [Inject] private MemoryScannerService ScannerService { get; set; } = default!;
        [Inject] private INotificationService NotificationService { get; set; } = default!;

        private ScanExecutor scanExecutor = default!;
        private MemoryValueHandler valueHandler = default!;
        private ValueFreezer valueFreezer = default!;
    }
}