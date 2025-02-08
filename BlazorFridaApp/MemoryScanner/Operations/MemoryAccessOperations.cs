using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public sealed class MemoryAccessOperations : MemoryScannerBase, IAsyncDisposable
    {
        private readonly IMemoryReaderService _memoryReader;
        private bool _disposed;
        private readonly SemaphoreSlim _semaphore;
        private const int MaxConcurrentOperations = 4;

        public MemoryAccessOperations(
            IMemoryReaderService memoryReader,
            ILogger<MemoryAccessOperations> logger) : base(logger)
        {
            _memoryReader = memoryReader;
            _semaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(MemoryAccessOperations));
            
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var buffer = RentBuffer(length);
                try
                {
                    var result = await ExecuteWithLogging(
                        async () =>
                        {
                            var data = await _memoryReader.ReadMemoryBytes(address, length);
                            Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
                            return buffer[..data.Length];
                        },
                        "Reading memory bytes",
                        address, length);

                    return result;
                }
                catch
                {
                    ReturnBuffer(buffer);
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task WriteMemory(nint address, byte[] value)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(MemoryAccessOperations));

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await ExecuteWithLogging(
                    () => _memoryReader.WriteMemoryBytes(address, value),
                    "Writing memory bytes",
                    address, value.Length);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await DisposeAsyncCore().ConfigureAwait(false);
                Dispose(disposing: false);
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private async ValueTask DisposeAsyncCore()
        {
            if (_memoryReader is IAsyncDisposable disposable)
            {
                try
                {
                    await disposable.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing memory reader");
                }
            }

            _semaphore.Dispose();
        }

        protected override void OnDispose()
        {
            _semaphore.Dispose();
        }
    }
}