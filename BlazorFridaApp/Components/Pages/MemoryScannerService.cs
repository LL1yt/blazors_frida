using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Components.Pages
{
    public class MemoryScannerService
    {
        private readonly IProcessService _processService;
        private readonly IScanProfileService _profileService;
        private readonly ILogger<MemoryScannerService> _logger;
        private readonly IAppNotificationService _notificationService;

        public MemoryScannerService(
            IProcessService processService,
            IScanProfileService profileService,
            ILogger<MemoryScannerService> logger,
            IAppNotificationService notificationService)
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
                _logger.LogInformation("Refreshing process list");
                var processes = await _processService.GetProcessesAsync();
                state.ProcessList = processes;
                _logger.LogInformation("Process list refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh process list");
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
            if (state.SelectedProcessId.HasValue)
            {
                await Task.CompletedTask; // Placeholder for future async operations
            }
        }

        public async Task OnScanTypeChanged(MemoryScannerState state, ScanType newType)
        {
            try
            {
                _logger.LogInformation($"Changing scan type from {state.SelectedScanType} to {newType}");
                state.SelectedScanType = newType;
                await Task.Run(() => state.Reset());
                
                await _notificationService.ShowInfo($"Selected scan type: {state.SelectedScanType}", "Scan type changed");
            }
            catch (Exception ex)
            {
                await _notificationService.ShowError(ex.Message, "Error changing scan type");
                throw;
            }
        }

        public async Task OnValueTypeChanged(MemoryScannerState state, MemoryValueType newType)
        {
            try
            {
                _logger.LogInformation($"Changing value type from {state.SelectedValueType} to {newType}");
                state.SelectedValueType = newType;
                state.Reset();
                
                await _notificationService.ShowInfo($"Selected value type: {state.SelectedValueType}", "Value type changed");
            }
            catch (Exception ex)
            {
                await _notificationService.ShowError(ex.Message, "Error changing value type");
                throw;
            }
        }
    }
}