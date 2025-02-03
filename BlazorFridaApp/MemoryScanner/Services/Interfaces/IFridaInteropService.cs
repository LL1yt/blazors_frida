namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IFridaInteropService : IAsyncDisposable
    {
        void Initialize();
        bool AttachToProcess(string processName);
        byte[]? ReadMemory(string address, int length);
        bool WriteMemory(string address, byte[] value);
        void Detach();
        Task DetachAsync();
    }
} 