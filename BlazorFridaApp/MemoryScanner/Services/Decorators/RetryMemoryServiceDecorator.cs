using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BlazorFridaApp.MemoryScanner.Services.Decorators;

public class RetryMemoryServiceDecorator : IMemoryReaderService
{
    private readonly IMemoryReaderService _inner;
    private readonly ILogger<RetryMemoryServiceDecorator> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public nint ProcessHandle => _inner.ProcessHandle;

    public RetryMemoryServiceDecorator(
        IMemoryReaderService inner,
        ILogger<RetryMemoryServiceDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
        _retryPolicy = CreateRetryPolicy();
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<InvalidOperationException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Attempt {RetryCount} failed. Retrying in {DelayMs}ms...",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    public void OpenProcess(int processId)
    {
        try
        {
            _inner.OpenProcess(processId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open process {ProcessId}", processId);
            throw;
        }
    }

    public async Task<byte[]> ReadMemoryBytes(nint address, int length)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                return await _inner.ReadMemoryBytes(address, length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading memory at address {Address}", address);
                throw;
            }
        });
    }

    public async Task WriteMemoryBytes(nint address, byte[] value)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                await _inner.WriteMemoryBytes(address, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing memory at address {Address}", address);
                throw;
            }
        });
    }

    public ValueTask DisposeAsync()
    {
        return _inner.DisposeAsync();
    }
}
