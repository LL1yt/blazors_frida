using BlazorFridaApp.Components.Pages;
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
    }
}