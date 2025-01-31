using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase, IAsyncDisposable
    {
        public required ScanExecutor scanExecutor;
        public required MemoryValueHandler valueHandler;
        public required ValueFreezer valueFreezer;

        [Inject]
        public required new NotificationService NotificationService { get; set; }
    }
}