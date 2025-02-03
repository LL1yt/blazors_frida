using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Components.Pages
{
    public abstract class MemoryScannerComponentBase : ComponentBase, IAsyncDisposable
    {
        [Inject] protected ILogger<MemoryScannerComponentBase> Logger { get; set; } = default!;
        [Inject] protected IProcessService ProcessService { get; set; } = default!;
        [Inject] protected IScanProfileService ProfileService { get; set; } = default!;
        [Inject] protected INotificationService NotificationService { get; set; } = default!;
        [Inject] protected MemoryScannerService ScannerService { get; set; } = default!;

        protected MemoryScannerState _state = new();

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            await ValueTask.CompletedTask;
        }

        public virtual async Task RefreshProcessList()
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnProcessSelected()
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnScanTypeChanged(ScanType newType)
        {
            await Task.CompletedTask;
        }

        public virtual void OnValueTypeChanged(MemoryValueType newType)
        {
        }

        public virtual async Task OnScan()
        {
            await Task.CompletedTask;
        }

        public virtual void OnScanComplete(List<IntPtr> results)
        {
        }

        public virtual void OnLoadingChanged(bool isLoading)
        {
        }

        public virtual async Task SaveConfig()
        {
            await Task.CompletedTask;
        }

        public virtual async Task LoadConfig()
        {
            await Task.CompletedTask;
        }

        public virtual async Task<byte[]> GetCurrentValue(nint address)
        {
            return Array.Empty<byte>();
        }

        public virtual bool IsFrozen(nint address)
        {
            return false;
        }

        public virtual async Task OnValueChanged(nint address, byte[] newValue)
        {
            await Task.CompletedTask;
        }

        public virtual async Task ToggleFreeze(nint address, byte[] value)
        {
            await Task.CompletedTask;
        }
    }
}