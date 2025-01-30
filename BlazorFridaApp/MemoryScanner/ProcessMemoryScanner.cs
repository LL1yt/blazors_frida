using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner
{
    public class ProcessMemoryScanner : IDisposable
    {
        private readonly IProcessService _processService;
        private readonly IMemoryReaderService _memoryReader;
        private readonly IMemoryScannerService _scanner;
        private readonly IValueFreezerService _freezer;
        private readonly IScanProfileService _profileService;
        private readonly ILogger<ProcessMemoryScanner> _logger;
        private bool _disposed;

        public ProcessMemoryScanner(
            IProcessService processService,
            IMemoryReaderService memoryReader,
            IMemoryScannerService scanner,
            IValueFreezerService freezer,
            IScanProfileService profileService,
            ILogger<ProcessMemoryScanner> logger)
        {
            _processService = processService;
            _memoryReader = memoryReader;
            _scanner = scanner;
            _freezer = freezer;
            _profileService = profileService;
            _logger = logger;
            _logger.LogInformation("ProcessMemoryScanner initialized");
        }

        public List<Process> GetProcesses() => _processService.GetAccessibleProcesses();

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            var matches = await _scanner.ScanForPattern(processId, pattern, mask);
            await _profileService.SaveScanResults(processId, pattern, mask, matches);
            return matches;
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
            => await _scanner.ScanForValue(processId, value, valueType);

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
            => await _scanner.GetAllAddresses(processId, valueType);

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
            => await _memoryReader.ReadMemoryBytes(address, length);

        public async Task WriteMemory(nint address, byte[] value)
            => await _memoryReader.WriteMemoryBytes(address, value);

        public async Task FreezeValue(nint address, byte[] value, string valueType)
            => await _freezer.FreezeValue(address, value, valueType);

        public async Task UnfreezeValue(nint address)
            => await _freezer.UnfreezeValue(address);

        public async Task SaveLastProcess(int processId)
            => await _profileService.SaveLastProcess(processId);

        public async Task<int?> GetLastProcessId()
            => await _profileService.GetLastProcessId();

        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryReader.Dispose();
                _freezer.Dispose();
                _disposed = true;
            }
        }
    }
}