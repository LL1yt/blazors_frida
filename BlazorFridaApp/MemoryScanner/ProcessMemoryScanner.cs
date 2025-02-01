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

        public List<Process> GetProcesses()
        {
            try
            {
                _logger.LogInformation("Getting accessible processes");
                return _processService.GetAccessibleProcesses().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible processes");
                throw;
            }
        }

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            try
            {
                _logger.LogInformation("Starting pattern scan for process {ProcessId}", processId);
                var matches = await _scanner.ScanForPattern(processId, pattern, mask);
                await _profileService.SaveScanResults(processId, pattern, mask, matches);
                _logger.LogInformation("Pattern scan completed. Found {MatchCount} matches", matches.Count);
                return matches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pattern scan for process {ProcessId}", processId);
                throw;
            }
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            try
            {
                _logger.LogInformation("Starting value scan for process {ProcessId}, value {Value}, type {ValueType}",
                    processId, value, valueType);
                var results = await _scanner.ScanForValue(processId, value, valueType);
                _logger.LogInformation("Value scan completed. Found {ResultCount} matches", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during value scan for process {ProcessId}", processId);
                throw;
            }
        }

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            try
            {
                _logger.LogInformation("Getting all addresses for process {ProcessId}, type {ValueType}",
                    processId, valueType);
                var addresses = await _scanner.GetAllAddresses(processId, valueType);
                _logger.LogInformation("Found {AddressCount} addresses", addresses.Count);
                return addresses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting addresses for process {ProcessId}", processId);
                throw;
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            try
            {
                _logger.LogDebug("Reading {Length} bytes from address {Address}", length, address);
                return await _memoryReader.ReadMemoryBytes(address, length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading memory at address {Address}", address);
                throw;
            }
        }

        public async Task WriteMemory(nint address, byte[] value)
        {
            try
            {
                _logger.LogInformation("Writing {Length} bytes to address {Address}", value.Length, address);
                await _memoryReader.WriteMemoryBytes(address, value);
                _logger.LogDebug("Memory write completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing memory at address {Address}", address);
                throw;
            }
        }

        public async Task FreezeValue(nint address, byte[] value, string valueType)
        {
            try
            {
                _logger.LogInformation("Freezing value at address {Address}, type {ValueType}", address, valueType);
                await _freezer.FreezeValue(address, value, valueType);
                _logger.LogInformation("Value frozen successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freezing value at address {Address}", address);
                throw;
            }
        }

        public async Task UnfreezeValue(nint address)
        {
            try
            {
                _logger.LogInformation("Unfreezing value at address {Address}", address);
                await _freezer.UnfreezeValue(address);
                _logger.LogInformation("Value unfrozen successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unfreezing value at address {Address}", address);
                throw;
            }
        }

        public async Task SaveLastProcess(int processId)
        {
            try
            {
                _logger.LogInformation("Saving last process ID: {ProcessId}", processId);
                await _profileService.SaveLastProcess(processId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving last process ID {ProcessId}", processId);
                throw;
            }
        }

        public async Task<int?> GetLastProcessId()
        {
            try
            {
                var processId = await _profileService.GetLastProcessId();
                _logger.LogInformation("Retrieved last process ID: {ProcessId}", processId);
                return processId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last process ID");
                throw;
            }
        }

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