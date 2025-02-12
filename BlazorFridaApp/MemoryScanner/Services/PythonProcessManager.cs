using System.Diagnostics;
using System.Runtime.InteropServices;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Proto.Health;

namespace BlazorFridaApp.MemoryScanner.Services;

public interface IPythonProcessManager : IDisposable
{
    Task EnsureServerRunning();
    Task StopServer();
    Task VerifyConnection();
    bool IsRunning { get; }
    int Port { get; }
}

public class PythonProcessManager : IPythonProcessManager, IDisposable
{
    private readonly ILogger<PythonProcessManager> _logger;
    private readonly int _port;
    private Process? _pythonProcess;
    private bool _disposed;
    private readonly GrpcChannelOptions _channelOptions;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private GrpcChannel? _healthChannel;
    private Health.HealthClient? _healthClient;
    private const int maxRetries = 10;
    private const int retryDelayMs = 500;
    private const int timeoutMs = 2000;

    public bool IsRunning => _pythonProcess != null && !_pythonProcess.HasExited;
    public int Port => _port;

    public PythonProcessManager(ILogger<PythonProcessManager> logger, int port)
    {
        _logger = logger;
        _port = port;
        _channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = null,
            MaxSendMessageSize = null
        };
    }

    private async Task<GrpcChannel> GetHealthChannelAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PythonProcessManager));

        await _connectionLock.WaitAsync();
        try
        {
            if (_healthChannel?.State == ConnectivityState.Ready)
                return _healthChannel;

            if (_healthChannel != null)
            {
                await _healthChannel.ShutdownAsync();
                _healthChannel.Dispose();
            }

            var endpoint = $"http://127.0.0.1:{_port}";
            _healthChannel = GrpcChannel.ForAddress(endpoint, _channelOptions);
            return _healthChannel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<Health.HealthClient> GetHealthClientAsync()
    {
        if (_healthClient == null)
        {
            var channel = await GetHealthChannelAsync();
            _healthClient = new Health.HealthClient(channel);
        }
        return _healthClient;
    }

    public async Task VerifyConnection()
    {
        _logger.LogInformation("[VerifyConnection] Starting connection verification to gRPC server on port {Port}", _port);
        var activity = new Activity("VerifyGrpcConnection").Start();
        
        const int maxRetries = 1; // Changed from 10 to 1
        const int timeoutMs = 5000;
        const int retryDelayMs = 500;
        var attempts = 0;
        Exception? lastException = null;

        while (attempts < maxRetries)
        {
            try
            {
                var client = await GetHealthClientAsync();
                var request = new HealthCheckRequest { Service = "" };
                
                using var cts = new CancellationTokenSource(timeoutMs);
                _logger.LogDebug("[VerifyConnection] Attempt {Attempt}: Sending health check request", attempts + 1);
                
                var response = await client.CheckAsync(request, 
                    deadline: DateTime.UtcNow.AddMilliseconds(timeoutMs),
                    cancellationToken: cts.Token);

                if (response.Status == HealthCheckResponse.Types.ServingStatus.Serving)
                {
                    _logger.LogInformation("[VerifyConnection] Successfully connected to gRPC server after {Attempts} attempts", attempts + 1);
                    activity?.Stop();
                    return;
                }
                
                _logger.LogWarning("[VerifyConnection] Server reported non-serving status: {Status}", response.Status);
                lastException = new InvalidOperationException($"Server reported non-serving status: {response.Status}");
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger.LogWarning("[VerifyConnection] Attempt {Attempt}: Server unavailable: {Message}", attempts + 1, ex.Message);
                lastException = ex;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning("[VerifyConnection] Attempt {Attempt}: Operation timed out: {Message}", attempts + 1, ex.Message);
                lastException = ex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VerifyConnection] Attempt {Attempt}: Unexpected error", attempts + 1);
                lastException = ex;
            }

            attempts++;
            if (attempts < maxRetries)
            {
                _logger.LogInformation("[VerifyConnection] Waiting {Delay}ms before next attempt", retryDelayMs);
                await Task.Delay(retryDelayMs);
            }
        }

        activity?.Stop();
        throw new InvalidOperationException(
            $"Failed to connect to gRPC server after {maxRetries} attempts. Last error: {lastException?.Message}", 
            lastException);
    }

    public Task EnsureServerRunning()
    {
        // We don't start the server anymore, just verify connection
        return VerifyConnection();
    }

    public Task StopServer()
    {
        // We don't manage the server anymore
        _pythonProcess = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_pythonProcess != null)
            {
                try
                {
                    if (!_pythonProcess.HasExited)
                    {
                        _pythonProcess.Kill();
                    }
                    _pythonProcess.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing Python process");
                }
            }

            if (_healthChannel != null)
            {
                try
                {
                    _healthChannel.ShutdownAsync().Wait();
                    _healthChannel.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing health channel");
                }
            }

            _connectionLock.Dispose();
            _disposed = true;
        }
    }
}

public class PythonServerException : Exception
{
    public PythonServerException(string message) : base(message) { }
    public PythonServerException(string message, Exception innerException) 
        : base(message, innerException) { }
}