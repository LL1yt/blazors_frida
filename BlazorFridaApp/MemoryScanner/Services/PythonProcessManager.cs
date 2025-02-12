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
    private readonly Health.HealthClient? _injectedHealthClient;
    private const int MaxRetries = 1;
    private const int RetryDelayMs = 200;
    private const int TimeoutMs = 500;

    public bool IsRunning => _pythonProcess != null && !_pythonProcess.HasExited;
    public int Port => _port;

    public PythonProcessManager(ILogger<PythonProcessManager> logger, int port, Health.HealthClient? healthClient = null)
    {
        _logger = logger;
        _port = port;
        _injectedHealthClient = healthClient;
        _channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = null,
            MaxSendMessageSize = null,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
            }
        };
        
        _logger.LogInformation("[Constructor] Initialized PythonProcessManager with port {Port}", port);
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
                try
                {
                    await _healthChannel.ShutdownAsync();
                    _healthChannel.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[GetHealthChannelAsync] Error shutting down existing channel");
                }
            }

            var endpoint = $"http://127.0.0.1:{_port}";
            var options = new GrpcChannelOptions
            {
                MaxReceiveMessageSize = null,
                MaxSendMessageSize = null,
                DisposeHttpClient = true,
                MaxRetryAttempts = 3,
                MaxRetryBufferSize = 1024 * 1024 * 5 // 5MB retry buffer
            };

            _healthChannel = GrpcChannel.ForAddress(endpoint, options);
            return _healthChannel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<Health.HealthClient> GetHealthClientAsync()
    {
        if (_injectedHealthClient != null)
            return _injectedHealthClient;

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
        
        var attempts = 0;
        Exception? lastException = null;

        while (attempts < MaxRetries)
        {
            try
            {
                _logger.LogDebug("[VerifyConnection] Getting health client...");
                var client = await GetHealthClientAsync();
                var request = new HealthCheckRequest { Service = "" };
                
                using var cts = new CancellationTokenSource(TimeoutMs);
                _logger.LogDebug("[VerifyConnection] Attempt {Attempt}: Sending health check request with timeout {Timeout}ms", 
                    attempts + 1, TimeoutMs);
                
                var response = await client.CheckAsync(request, 
                    deadline: DateTime.UtcNow.AddMilliseconds(TimeoutMs),
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
            catch (RpcException ex)
            {
                _logger.LogWarning("[VerifyConnection] Attempt {Attempt}: RPC error: {StatusCode} - {Message}", 
                    attempts + 1, ex.StatusCode, ex.Message);
                lastException = ex;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning("[VerifyConnection] Attempt {Attempt}: Operation timed out after {Timeout}ms: {Message}", 
                    attempts + 1, TimeoutMs, ex.Message);
                lastException = ex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VerifyConnection] Attempt {Attempt}: Unexpected error", attempts + 1);
                lastException = ex;
            }

            attempts++;
            if (attempts < MaxRetries)
            {
                _logger.LogInformation("[VerifyConnection] Waiting {Delay}ms before next attempt", RetryDelayMs);
                await Task.Delay(RetryDelayMs);
            }
        }

        activity?.Stop();
        throw new InvalidOperationException(
            $"Failed to connect to gRPC server after {MaxRetries} attempts. Last error: {lastException?.Message}", 
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