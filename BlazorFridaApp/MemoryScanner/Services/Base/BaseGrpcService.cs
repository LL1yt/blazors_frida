using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;

namespace BlazorFridaApp.MemoryScanner.Services.Base;

public abstract class BaseGrpcService : IDisposable, IAsyncDisposable
{
    protected readonly ILogger _logger;
    protected readonly IPythonProcessManager _processManager;
    protected readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    protected static readonly TextMapPropagator Propagator = new TraceContextPropagator();

    protected BaseGrpcService(
        ILogger logger,
        IPythonProcessManager processManager)
    {
        _logger = logger;
        _processManager = processManager;
    }

    protected async Task<GrpcChannel> GetChannelAsync()
    {
        await _processManager.EnsureServerRunning();
        var channelKey = $"localhost:{_processManager.Port}";

        return _channels.GetOrAdd(channelKey, key =>
        {
            var channel = GrpcChannel.ForAddress($"http://{key}");
            return channel;
        });
    }

    protected virtual Proto.MemoryScanner.MemoryScannerClient CreateClient(GrpcChannel channel)
    {
        return new Proto.MemoryScanner.MemoryScannerClient(channel);
    }

    protected Metadata CreateMetadata()
    {
        var metadata = new Metadata();
        var activity = System.Diagnostics.Activity.Current;
        if (activity != null)
        {
            metadata.Add("traceparent", $"00-{activity.TraceId}-{activity.SpanId}-01");
        }
        return metadata;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
        {
            await channel.ShutdownAsync();
        }
        _channels.Clear();
    }

    public void Dispose()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
        _channels.Clear();
        GC.SuppressFinalize(this);
    }
}