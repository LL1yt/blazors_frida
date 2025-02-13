using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using OpenTelemetry.Context.Propagation;
using Grpc.Core;
using System.Diagnostics;

namespace BlazorFridaApp.Tests.Infrastructure;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IPythonProcessManager ProcessManager;
    protected readonly ILogger<IntegrationTestBase> Logger;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly GrpcChannelOptions _channelOptions;
    protected static readonly TextMapPropagator Propagator = new TraceContextPropagator();
    private bool _disposed;
    private const int MaxConnectionAttempts = 1;
    private const int ConnectionRetryDelayMs = 200;
    protected virtual int ConnectionTimeoutMs { get; } = 500; // 5 секунд

    public IntegrationTestBase()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
        Logger = factory.CreateLogger<IntegrationTestBase>();
        Logger.LogInformation("[IntegrationTestBase] Constructor started");
        
        var processManagerLogger = factory.CreateLogger<PythonProcessManager>();
        ProcessManager = new PythonProcessManager(processManagerLogger, 50051); // Hardcoded port since we're connecting to existing server

        _channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = null, // Remove message size limits
            MaxSendMessageSize = null,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
            }
        };

        Logger.LogInformation("[IntegrationTestBase] Created PythonProcessManager instance");
    }

    protected async Task<GrpcChannel> GetChannelAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(IntegrationTestBase));

        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var endpoint = $"http://127.0.0.1:{ProcessManager.Port}";
            if (_channels.TryGetValue(endpoint, out var existingChannel))
            {
                if (existingChannel.State != ConnectivityState.Shutdown)
                {
                    return existingChannel;
                }
                
                if (_channels.TryRemove(endpoint, out var oldChannel))
                {
                    await oldChannel.ShutdownAsync().ConfigureAwait(false);
                }
            }

            var channel = GrpcChannel.ForAddress(endpoint, _channelOptions);
            if (_channels.TryAdd(endpoint, channel))
            {
                return channel;
            }

            await channel.ShutdownAsync().ConfigureAwait(false);
            return _channels[endpoint];
        }
        finally
        {
            _channelLock.Release();
        }
    }

    protected Metadata CreateMetadata()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(IntegrationTestBase));

        var metadata = new Metadata();
        var context = Activity.Current?.Context ?? default;
        Propagator.Inject(new PropagationContext(context, default), metadata,
            (m, k, v) => m.Add(k, v));
        return metadata;
    }

    public virtual async Task InitializeAsync()
    {
        Logger.LogInformation("[IntegrationTestBase] InitializeAsync started");
        
        for (int attempt = 1; attempt <= MaxConnectionAttempts; attempt++)
        {
            try
            {
                Logger.LogInformation("[InitializeAsync] Connection attempt {Attempt} of {MaxAttempts} to port {Port}", 
                    attempt, MaxConnectionAttempts, ProcessManager.Port);
                
                await ProcessManager.VerifyConnection(ConnectionTimeoutMs);
                Logger.LogInformation("[InitializeAsync] Successfully connected to gRPC server on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[InitializeAsync] Connection attempt {Attempt} failed. Error details: {ErrorMessage}", 
                    attempt, ex.ToString());
                
                if (attempt == MaxConnectionAttempts)
                {
                    Logger.LogError("[InitializeAsync] All connection attempts failed after {MaxAttempts} tries", MaxConnectionAttempts);
                    throw new InvalidOperationException(
                        $"Failed to connect to gRPC server after {MaxConnectionAttempts} attempts. Please ensure the server is running by executing 'start_test_server.bat' before running tests. Last error: {ex.Message}", 
                        ex);
                }
                
                Logger.LogInformation("[InitializeAsync] Waiting {Delay}ms before next attempt", ConnectionRetryDelayMs);
                await Task.Delay(ConnectionRetryDelayMs);
            }
        }
    }

    public virtual async Task DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                Logger.LogInformation("[IntegrationTestBase] DisposeAsync started");
                
                foreach (var channel in _channels.Values)
                {
                    try
                    {
                        await channel.ShutdownAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Error shutting down gRPC channel");
                    }
                }
                _channels.Clear();
                _channelLock.Dispose();

                if (ProcessManager is IDisposable disposable)
                {
                    disposable.Dispose();
                    Logger.LogInformation("[IntegrationTestBase] ProcessManager disposed");
                }
                
                Logger.LogInformation("[IntegrationTestBase] DisposeAsync completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[IntegrationTestBase] Error during test cleanup");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}