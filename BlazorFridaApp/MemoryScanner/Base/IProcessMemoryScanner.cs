using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Base
{
    public interface IProcessMemoryScanner : IAsyncDisposable
    {
        List<ProcessInfo> GetProcesses();
        Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask);
        Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType);
        Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType);
        Task<byte[]> ReadMemoryBytes(nint address, int length);
        Task WriteMemory(nint address, byte[] value);
        Task FreezeValue(nint address, byte[] value, string valueType);
        Task UnfreezeValue(nint address);
        Task SaveLastProcess(int processId);
        Task<int?> GetLastProcessId();
    }
}