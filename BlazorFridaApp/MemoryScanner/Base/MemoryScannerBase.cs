using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Base
{
    public abstract class MemoryScannerBase
    {
        protected readonly ILogger _logger;

        protected MemoryScannerBase(ILogger logger)
        {
            _logger = logger;
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
    }
}