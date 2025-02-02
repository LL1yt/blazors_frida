namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IValueFreezerService : IMemoryScannerBaseService, IAsyncDisposable
    {
        Task FreezeValue(nint address, byte[] value, string valueType);
        Task UnfreezeValue(nint address);
    }
}