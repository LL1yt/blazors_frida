using BlazorFridaApp.Components.Pages;
using BlazorFridaApp.MemoryScanner.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IScanProfileService
    {
        Task<Dictionary<string, ScannerConfig>> GetScannerConfigs();
        Task SaveScannerConfig(string name, ScannerConfig config);
        Task DeleteScannerConfig(string name);
        Task SaveLastProcess(int processId);
        Task<int?> GetLastProcessId();
        Task SaveScanResults(IEnumerable<ScanResult> results);
        Task<ScanProfile> SaveProfileAsync(ScanProfile profile);
        Task<ScanProfile> GetProfileAsync(string name);
        List<string> ValidateProfile(ScanProfile profile);
    }
}