namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IScanProfileService : IMemoryScannerBaseService
    {
        Task SaveScanResults(int processId, byte[] pattern, string mask, List<nint> addresses);
        Task SaveLastProcess(int processId);
        Task<int?> GetLastProcessId();
    }
}