using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IScanProfileService
    {
        ScanProfile GetCurrentProfile();
        Task SaveScanResults(int processId, byte[] pattern, string mask, List<nint> matches);
        Task SaveLastProcess(int processId);
        Task<int?> GetLastProcessId();
    }
}