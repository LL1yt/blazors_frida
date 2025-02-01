using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Services.Extensions;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class MemoryScannerCoordinator
    {
        private readonly IProcessService _processService;
        private readonly IMemoryScannerService _memoryScannerService;
        private readonly IMemoryReaderService _memoryReaderService;
        private readonly IScanProfileService _scanProfileService;
        private readonly IValueFreezerService _valueFreezerService;

        public MemoryScannerCoordinator(
            IProcessService processService,
            IMemoryScannerService memoryScannerService,
            IMemoryReaderService memoryReaderService,
            IScanProfileService scanProfileService,
            IValueFreezerService valueFreezerService)
        {
            _processService = processService;
            _memoryScannerService = memoryScannerService;
            _memoryReaderService = memoryReaderService;
            _scanProfileService = scanProfileService;
            _valueFreezerService = valueFreezerService;
        }

        public async Task<IEnumerable<string>> ScanMemoryAsync(string searchPattern, int scanType)
        {
            var process = await _processService.GetTargetProcessAsync();
            if (process == null)
            {
                throw new InvalidOperationException("No target process selected.");
            }

            ScanProfile profile = BlazorFridaApp.MemoryScanner.Services.Extensions.ScanProfileServiceExtensions.GetCurrentProfile(_scanProfileService);
            return await BlazorFridaApp.MemoryScanner.Services.Extensions.MemoryScannerServiceExtensions.ScanAsync(_memoryScannerService, process, searchPattern, scanType, profile);
        }
    }
}