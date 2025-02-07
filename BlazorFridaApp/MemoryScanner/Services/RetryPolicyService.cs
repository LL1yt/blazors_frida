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
        if (typeof(T) == null)
        {
            throw new ArgumentNullException(nameof(T), "Generic type parameter cannot be null");
        }

        return Policy<T>
            .Handle<SocketException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(InitialDelayMs * Math.Pow(2, retryAttempt - 1)),
                (result, duration, retryCount, context) =>
                {
                    if (result.Exception != null)
                    {
                        var logMessage = $"Attempt {retryCount} of {MaxRetries} failed. Retrying in {duration.TotalMilliseconds}ms...";
                        _logger.LogWarning(result.Exception, logMessage);
                    }
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
                (exception, duration, retryCount, context) =>
                {
                    var logMessage = $"Attempt {retryCount} of {MaxRetries} failed. Retrying in {duration.TotalMilliseconds}ms...";
                    _logger.LogWarning(exception, logMessage);
                });
    }
}
