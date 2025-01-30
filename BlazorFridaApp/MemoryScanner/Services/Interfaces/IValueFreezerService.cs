namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IValueFreezerService : IMemoryScannerBaseService, IDisposable
    {
        Task FreezeValue(nint address, byte[] value, string valueType);
        Task UnfreezeValue(nint address);
    }
}