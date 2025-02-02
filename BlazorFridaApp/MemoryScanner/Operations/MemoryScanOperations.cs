using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public class MemoryScanOperations : MemoryScannerBase
    {
        private readonly IMemoryScannerService _scanner;
        private readonly IScanProfileService _profileService;

        public MemoryScanOperations(
            IMemoryScannerService scanner,
            IScanProfileService profileService,
            ILogger<MemoryScanOperations> logger) : base(logger)
        {
            _scanner = scanner;
            _profileService = profileService;
        }

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            return await ExecuteWithLogging(
                async () =>
                {
                    var matches = await _scanner.ScanForPattern(processId, pattern, mask);
                    await _profileService.SaveScanResults(processId, pattern, mask, matches);
                    return matches;
                },
                "Pattern scan",
                processId);
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            return await ExecuteWithLogging(
                () => _scanner.ScanForValue(processId, value, valueType),
                "Value scan",
                processId, value, valueType);
        }

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            return await ExecuteWithLogging(
                () => _scanner.GetAllAddresses(processId, valueType),
                "Getting all addresses",
                processId, valueType);
        }
    }
}