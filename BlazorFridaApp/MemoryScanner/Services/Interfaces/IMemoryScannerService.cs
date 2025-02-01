using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IMemoryScannerService
    {
        Task<IEnumerable<string>> ScanAsync(Process process, string searchPattern, int scanType, ScanProfile profile);
        Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask);
        Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType);
        Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType);
    }
}