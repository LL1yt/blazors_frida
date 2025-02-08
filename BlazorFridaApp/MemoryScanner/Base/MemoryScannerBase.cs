using Microsoft.Extensions.Logging;
using System.Buffers;

namespace BlazorFridaApp.MemoryScanner.Base
{
    public abstract class MemoryScannerBase : IDisposable
    {
        protected readonly ILogger _logger;
        private bool _disposed;
        protected readonly ArrayPool<byte> _arrayPool;
        protected const int DefaultBufferSize = 4096;

        protected MemoryScannerBase(ILogger logger)
        {
            _logger = logger;
            _arrayPool = ArrayPool<byte>.Shared;
        }

        protected async Task<T> ExecuteWithLogging<T>(
            Func<Task<T>> operation,
            string operationName,
            params object[] logParams)
        {
            try
            {
                _logger.LogInformation($"Starting {operationName}", logParams);
                var result = await operation();
                _logger.LogInformation($"Completed {operationName}", logParams);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during {operationName}", logParams);
                throw;
            }
        }

        protected async Task ExecuteWithLogging(
            Func<Task> operation,
            string operationName,
            params object[] logParams)
        {
            try
            {
                _logger.LogInformation($"Starting {operationName}", logParams);
                await operation();
                _logger.LogInformation($"Completed {operationName}", logParams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during {operationName}", logParams);
                throw;
            }
        }

        protected byte[] RentBuffer(int minimumLength)
        {
            return _arrayPool.Rent(minimumLength);
        }

        protected void ReturnBuffer(byte[] buffer)
        {
            if (buffer != null)
            {
                _arrayPool.Return(buffer);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    OnDispose();
                }
                _disposed = true;
            }
        }

        protected virtual void OnDispose()
        {
            // Override in derived classes to clean up resources
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}