using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services.Interfaces;

namespace BlazorFridaApp.Services
{
    public class MemoryCleanupService : IMemoryCleanupService
    {
        private readonly IMemoryReaderService _memoryReader;
        private readonly IProcessService _processService;
        private readonly ILogger<MemoryCleanupService> _logger;

        public MemoryCleanupService(
            IMemoryReaderService memoryReader,
            IProcessService processService,
            ILogger<MemoryCleanupService> logger)
        {
            _memoryReader = memoryReader;
            _processService = processService;
            _logger = logger;
        }

        public async Task CleanupAsync()
        {
            try
            {
                if (_memoryReader is IAsyncDisposable memoryReaderDisposable)
                {
                    await memoryReaderDisposable.DisposeAsync();
                }

                if (_processService is IAsyncDisposable processServiceDisposable)
                {
                    await processServiceDisposable.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory cleanup");
            }
        }
    }
}