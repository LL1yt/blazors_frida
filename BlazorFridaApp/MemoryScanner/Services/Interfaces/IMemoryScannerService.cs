using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IMemoryScannerService : IMemoryScannerBaseService
    {
        Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask);
        Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType);
        Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType);
    }
}