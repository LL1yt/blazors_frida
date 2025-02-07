using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Exceptions;
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

    private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, string operationName)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {Operation} after all retry attempts", operationName);
            throw new MemoryScanException($"Failed to execute {operationName}: {ex.Message}", ex);
        }
    }

    private async Task ExecuteWithRetry(Func<Task> operation, string operationName)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {Operation} after all retry attempts", operationName);
            throw new MemoryScanException($"Failed to execute {operationName}: {ex.Message}", ex);
        }
    }

    public void OpenProcess(int processId)
    {
        try
        {
            _inner.OpenProcess(processId);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ProcessAccessException($"Access denied when trying to open process {processId}", ex);
        }
    }

    public async Task<byte[]> ReadMemoryBytes(IntPtr address, int size)
    {
        try
        {
            return await ExecuteWithRetry(
                () => _inner.ReadMemoryBytes(address, size),
                "ReadMemoryBytes");
        }
        catch (Exception ex) when (ex is not MemoryScanException)
        {
            throw new MemoryScanException($"Failed to read memory at address {address}: {ex.Message}", ex);
        }
    }

    public async Task WriteMemoryBytes(IntPtr address, byte[] bytes)
    {
        try
        {
            await ExecuteWithRetry(
                () => _inner.WriteMemoryBytes(address, bytes),
                "WriteMemoryBytes");
        }
        catch (Exception ex) when (ex is not MemoryWriteException)
        {
            throw new MemoryWriteException($"Failed to write memory at address {address}: {ex.Message}", ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _inner.DisposeAsync();
    }
}
