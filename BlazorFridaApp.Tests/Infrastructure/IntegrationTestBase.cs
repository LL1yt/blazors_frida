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
        try
        {
            await Task.Delay(100); // Small delay before first connection attempt
            Logger.LogInformation("[IntegrationTestBase] Verifying gRPC server connection");
            await ProcessManager.VerifyConnection();
            Logger.LogInformation("[IntegrationTestBase] Successfully connected to gRPC server");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[IntegrationTestBase] Failed to connect to gRPC server. Make sure the server is running on port 50051");
            throw new InvalidOperationException("Failed to connect to gRPC server. Make sure to start the server manually before running tests.", ex);
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