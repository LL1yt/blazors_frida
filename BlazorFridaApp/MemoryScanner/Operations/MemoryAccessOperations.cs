using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public class MemoryAccessOperations : MemoryScannerBase, IDisposable
    {
        private readonly IMemoryReaderService _memoryReader;
        private bool _disposed;

        public MemoryAccessOperations(
            IMemoryReaderService memoryReader,
            ILogger<MemoryAccessOperations> logger) : base(logger)
        {
            _memoryReader = memoryReader;
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            return await ExecuteWithLogging(
                () => _memoryReader.ReadMemoryBytes(address, length),
                "Reading memory bytes",
                address, length);
        }

        public async Task WriteMemory(nint address, byte[] value)
        {
            await ExecuteWithLogging(
                () => _memoryReader.WriteMemoryBytes(address, value),
                "Writing memory bytes",
                address, value.Length);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryReader.Dispose();
                _disposed = true;
            }
        }
    }
}