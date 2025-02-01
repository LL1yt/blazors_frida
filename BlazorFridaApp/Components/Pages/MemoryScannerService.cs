using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;

namespace BlazorFridaApp.Components.Pages
{
    public class MemoryScannerService
    {
        private readonly IProcessService _processService;
        private readonly IScanProfileService _profileService;
        private readonly ILogger<MemoryScannerService> _logger;
        private readonly INotificationService _notificationService;

        public MemoryScannerService(
            IProcessService processService,
            IScanProfileService profileService,
            ILogger<MemoryScannerService> logger,
            INotificationService notificationService)
        {
            _processService = processService;
            _profileService = profileService;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task RefreshProcessList(MemoryScannerState state)
        {
            try
            {
                _logger.LogInformation("Refreshing process list...");
                state.ProcessList = _processService.GetAccessibleProcesses().ToList();
                
                if (!state.ProcessList.Any())
                {
                    _notificationService.ShowWarning("No processes found",
                        "No accessible processes were found");
                    return;
                }
                
                _logger.LogInformation($"Successfully loaded {state.ProcessList.Count} processes");
                await LoadLastProcess(state);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to refresh process list",
                    "Failed to load process list", ex);
                throw;
            }
        }

        public async Task LoadLastProcess(MemoryScannerState state)
        {
            var lastId = await _profileService.GetLastProcessId();
            if (lastId.HasValue)
            {
                state.SelectedProcessId = lastId.Value;
                await OnProcessSelected(state);
            }
        }

        public async Task OnProcessSelected(MemoryScannerState state)
        {
            try
            {
                if (state.SelectedProcessId.HasValue)
                {
                    await _profileService.SaveLastProcess(state.SelectedProcessId.Value);
                    state.Reset();
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error selecting process", ex.Message, ex);
                throw;
            }
        }

        public async Task OnScanTypeChanged(MemoryScannerState state, ScanType newType)
        {
            try
            {
                _logger.LogInformation($"Changing scan type from {state.SelectedScanType} to {newType}");
                state.SelectedScanType = newType;
                state.Reset();
                
                _notificationService.ShowInfo("Scan type changed", $"Selected scan type: {state.SelectedScanType}");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error changing scan type", ex.Message, ex);
                throw;
            }
        }

        public void OnValueTypeChanged(MemoryScannerState state, MemoryValueType newType)
        {
            try
            {
                _logger.LogInformation($"Changing value type from {state.SelectedValueType} to {newType}");
                state.SelectedValueType = newType;
                state.Reset();
                
                _notificationService.ShowInfo("Value type changed", $"Selected value type: {state.SelectedValueType}");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error changing value type", ex.Message, ex);
                throw;
            }
        }
    }
}