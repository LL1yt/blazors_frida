using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Models;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Components;

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

        public virtual void OnScanComplete(List<nint> results)
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

        public virtual Task<byte[]> GetCurrentValue(nint address)
        {
            return Task.FromResult(new byte[0]);
        }

        public virtual bool IsFrozen(nint address)
        {
            return false;
        }

        public virtual Task OnValueChanged(nint address, byte[] newValue)
        {
            return Task.CompletedTask;
        }

        public virtual Task ToggleFreeze(nint address, byte[] value)
        {
            return Task.CompletedTask;
        }
    }
}