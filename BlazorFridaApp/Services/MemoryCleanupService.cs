using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.Services
{
    public interface IMemoryCleanupService
    {
        Task CleanupAsync();
    }

    public class MemoryCleanupService : IMemoryCleanupService
    {
        private readonly IFridaInteropService _fridaInterop;
        private readonly IPythonRuntimeService _pythonRuntime;
        private readonly ILogger<MemoryCleanupService> _logger;

        public MemoryCleanupService(
            IFridaInteropService fridaInterop,
            IPythonRuntimeService pythonRuntime,
            ILogger<MemoryCleanupService> logger)
        {
            _fridaInterop = fridaInterop;
            _pythonRuntime = pythonRuntime;
            _logger = logger;
        }

        public async Task CleanupAsync()
        {
            try
            {
                await _fridaInterop.DetachAsync();
                _pythonRuntime.ReleaseGIL();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory cleanup");
            }
        }
    }
}