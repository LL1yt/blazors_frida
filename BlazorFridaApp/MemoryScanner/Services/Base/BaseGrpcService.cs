using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace BlazorFridaApp.MemoryScanner.Services.Base;

public abstract class BaseGrpcService : IDisposable, IAsyncDisposable
{
    protected readonly ILogger _logger;
    protected readonly IPythonProcessManager _processManager;
    protected readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    protected static readonly TextMapPropagator Propagator = new TraceContextPropagator();
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly GrpcChannelOptions _channelOptions;
    private bool _disposed;

    protected BaseGrpcService(
        ILogger logger,
        IPythonProcessManager processManager)
    {
        _logger = logger;
        _processManager = processManager;
        _channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 1024 * 1024 * 50, // 50MB
            MaxSendMessageSize = 1024 * 1024 * 50,    // 50MB
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this.GetType().Name);
    }

    protected async Task<GrpcChannel> GetChannelAsync()
    {
        ThrowIfDisposed();

        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var endpoint = $"http://[::1]:{_processManager.Port}";  // Use IPv6 loopback
            if (_channels.TryGetValue(endpoint, out var existingChannel))
            {
                if (existingChannel.State != ConnectivityState.Shutdown)
                {
                    return existingChannel;
                }
                
                // Remove and dispose shutdown channel
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

            // Another thread created the channel first
            await channel.ShutdownAsync().ConfigureAwait(false);
            return _channels[endpoint];
        }
        finally
        {
            _channelLock.Release();
        }
    }

    protected virtual Proto.MemoryScanner.MemoryScannerClient CreateClient(GrpcChannel channel)
    {
        ThrowIfDisposed();
        return new Proto.MemoryScanner.MemoryScannerClient(channel);
    }

    protected Metadata CreateMetadata()
    {
        ThrowIfDisposed();
        var metadata = new Metadata();
        var context = Activity.Current?.Context ?? default;
        // Only inject trace context without baggage
        Propagator.Inject(new PropagationContext(context, default), metadata,
            (m, k, v) => m.Add(k, v));
        return metadata;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        foreach (var channel in _channels.Values)
        {
            try
            {
                await channel.ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error shutting down gRPC channel");
            }
        }
        _channels.Clear();
        _channelLock.Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var channel in _channels.Values)
                {
                    try
                    {
                        channel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing gRPC channel");
                    }
                }
                _channels.Clear();
                _channelLock.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}