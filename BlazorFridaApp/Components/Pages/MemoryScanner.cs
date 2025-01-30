using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase, IAsyncDisposable
    {
        private ScanExecutor scanExecutor;
        private MemoryValueHandler valueHandler;
        private ValueFreezer valueFreezer;
    }
}