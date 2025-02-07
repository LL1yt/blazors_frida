using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BlazorFridaApp.MemoryScanner.Services;

public class RetryPolicyService
{
    private readonly ILogger<RetryPolicyService> _logger;
    private const int MaxRetries = 3;
    private const int InitialDelayMs = 100;

    public RetryPolicyService(ILogger<RetryPolicyService> logger)
    {
        _logger = logger;
    }

    public AsyncRetryPolicy<T> CreateAsyncRetryPolicy<T>()
    {
        return Policy<T>
            .Handle<SocketException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(InitialDelayMs * Math.Pow(2, retryAttempt - 1)),
                (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Attempt {RetryCount} of {MaxRetries} failed. Retrying in {DelayMs}ms...",
                        retryCount,
                        MaxRetries,
                        timeSpan.TotalMilliseconds);
                });
    }

    public AsyncRetryPolicy CreateAsyncRetryPolicy()
    {
        return Policy
            .Handle<SocketException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(InitialDelayMs * Math.Pow(2, retryAttempt - 1)),
                (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Attempt {RetryCount} of {MaxRetries} failed. Retrying in {DelayMs}ms...",
                        retryCount,
                        MaxRetries,
                        timeSpan.TotalMilliseconds);
                });
    }
}
