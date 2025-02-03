using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;

namespace BlazorFridaApp.Components.Pages
{
    public abstract class MemoryScannerComponentBase : ComponentBase, IAsyncDisposable
    {
        [Inject] protected ILogger<MemoryScannerComponentBase> Logger { get; set; } = default!;
        [Inject] protected IProcessService ProcessService { get; set; } = default!;
        [Inject] protected IScanProfileService ProfileService { get; set; } = default!;
        [Inject] protected INotificationService NotificationService { get; set; } = default!;
        [Inject] protected MemoryScannerService ScannerService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
        }

        public virtual async ValueTask DisposeAsync()
        {
            await ValueTask.CompletedTask;
        }
    }
} 