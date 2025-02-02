using System.Runtime.InteropServices;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaMemoryService : IMemoryReaderService
    {
        private readonly ILogger<FridaMemoryService> _logger;
        private readonly IFridaInteropService _fridaInterop;
        private bool _disposed;
        private string _processName = string.Empty;

        public nint ProcessHandle { get; private set; }

        public FridaMemoryService(
            ILogger<FridaMemoryService> logger,
            IFridaInteropService fridaInterop)
        {
            _logger = logger;
            _fridaInterop = fridaInterop;
            _fridaInterop.Initialize();
        }

        public void OpenProcess(int processId)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                _processName = process.ProcessName;

                if (!_fridaInterop.AttachToProcess(_processName))
                {
                    throw new MemoryOperationException($"Failed to attach to process {_processName}");
                }

                ProcessHandle = process.Handle;
            }
            catch (Exception ex) when (ex is not MemoryOperationException)
            {
                _logger.LogError(ex, $"Failed to open process {processId}");
                throw new MemoryOperationException($"Failed to open process {processId}", ex);
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            try
            {
                var result = _fridaInterop.ReadMemory(address.ToString(), length);
                if (result == null)
                {
                    throw new MemoryOperationException($"Failed to read memory at address {address}");
                }
                return result;
            }
            catch (Exception ex) when (ex is not MemoryOperationException)
            {
                _logger.LogError(ex, $"Failed to read memory at address {address}");
                throw new MemoryOperationException($"Failed to read memory at address {address}", ex);
            }
        }

        public async Task WriteMemoryBytes(nint address, byte[] value)
        {
            try
            {
                if (!_fridaInterop.WriteMemory(address.ToString(), value))
                {
                    throw new MemoryOperationException($"Failed to write memory at address {address}");
                }
            }
            catch (Exception ex) when (ex is not MemoryOperationException)
            {
                _logger.LogError(ex, $"Failed to write memory at address {address}");
                throw new MemoryOperationException($"Failed to write memory at address {address}", ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _fridaInterop.DisposeAsync();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }

    public class MemoryOperationException : Exception
    {
        public MemoryOperationException(string message) : base(message) { }
        public MemoryOperationException(string message, Exception innerException) : base(message, innerException) { }
    }
}