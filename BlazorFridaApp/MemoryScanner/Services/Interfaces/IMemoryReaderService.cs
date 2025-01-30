namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IMemoryReaderService : IMemoryScannerBaseService, IDisposable
    {
        nint ProcessHandle { get; }
        void OpenProcess(int processId);
        Task<byte[]> ReadMemoryBytes(nint address, int length);
        Task WriteMemoryBytes(nint address, byte[] value);
    }
}