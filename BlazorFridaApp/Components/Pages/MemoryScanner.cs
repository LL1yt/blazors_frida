using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Components;

namespace BlazorFridaApp.Components.Pages
{
    public partial class MemoryScanner : MemoryScannerComponentBase, IAsyncDisposable
    {
        protected ScanExecutor scanExecutor;
        protected MemoryValueHandler valueHandler;
        protected ValueFreezer valueFreezer;
    }
}