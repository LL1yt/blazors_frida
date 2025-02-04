using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace BlazorFridaApp.MemoryScanner.Services;

public interface IMemoryScannerGrpcService
{
    Task<IEnumerable<Models.ProcessInfo>> ListProcessesAsync();
    Task<(bool success, string sessionId)> AttachToProcessAsync(int pid);
    Task DetachFromProcessAsync(string sessionId);
    Task<IEnumerable<Models.ScanResult>> ScanMemoryAsync(string sessionId, string valueType, byte[] value, string comparisonType, IEnumerable<(ulong start, ulong end)> ranges);
    Task<(byte[] value, bool success, string error)> ReadMemoryAsync(string sessionId, ulong address, int size, string valueType);
    Task<(bool success, string error)> WriteMemoryAsync(string sessionId, ulong address, byte[] value, string valueType);
    IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(string sessionId, ulong address, byte[] value, string valueType, CancellationToken cancellationToken);
    Task UnfreezeValueAsync(string sessionId, ulong address);
    Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(string sessionId, string checkpointId);
    Task<(bool success, string error, string newVersion)> SyncStateAsync(string sessionId, Dictionary<string, byte[]> stateUpdates, string version);
}

public class MemoryScannerGrpcService : IMemoryScannerGrpcService, IProcessService, IMemoryReaderService, IMemoryScannerService, IDisposable
{
    private readonly ILogger<MemoryScannerGrpcService> _logger;
    private readonly IPythonProcessManager _processManager;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

    public MemoryScannerGrpcService(
        ILogger<MemoryScannerGrpcService> logger,
        IPythonProcessManager processManager)
    {
        _logger = logger;
        _processManager = processManager;
    }

    private async Task<GrpcChannel> GetChannelAsync()
    {
        await _processManager.EnsureServerRunning();
        var channelKey = $"localhost:{_processManager.Port}";

        return _channels.GetOrAdd(channelKey, key =>
        {
            var channel = GrpcChannel.ForAddress($"http://{key}");
            return channel;
        });
    }

    private Metadata CreateMetadata()
    {
        var metadata = new Metadata();
        var activity = System.Diagnostics.Activity.Current;
        if (activity != null)
        {
            metadata.Add("traceparent", $"00-{activity.TraceId}-{activity.SpanId}-01");
        }
        return metadata;
    }

    public async Task<IEnumerable<Models.ProcessInfo>> ListProcessesAsync()
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return new List<Models.ProcessInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list processes");
            throw;
        }
    }

    public async Task<(bool success, string sessionId)> AttachToProcessAsync(int pid)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return (true, Guid.NewGuid().ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to process {Pid}", pid);
            return (false, string.Empty);
        }
    }

    public async Task DetachFromProcessAsync(string sessionId)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detach from process {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<IEnumerable<Models.ScanResult>> ScanMemoryAsync(
        string sessionId,
        string valueType,
        byte[] value,
        string comparisonType,
        IEnumerable<(ulong start, ulong end)> ranges)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return new List<Models.ScanResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan memory for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<(byte[] value, bool success, string error)> ReadMemoryAsync(
        string sessionId,
        ulong address,
        int size,
        string valueType)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return (new byte[0], true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read memory at {Address} for session {SessionId}", address, sessionId);
            return (new byte[0], false, ex.Message);
        }
    }

    public async Task<(bool success, string error)> WriteMemoryAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write memory at {Address} for session {SessionId}", address, sessionId);
            return (false, ex.Message);
        }
    }

    private async IAsyncEnumerable<(bool active, byte[] currentValue, string error)> StreamFreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync();
        // TODO: Replace with actual gRPC streaming call once code is generated
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
            yield return (true, value, string.Empty);
        }
    }

    public IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken)
    {
        return StreamFreezeValueAsync(sessionId, address, value, valueType, cancellationToken);
    }

    public async Task UnfreezeValueAsync(string sessionId, ulong address)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unfreeze value at {Address} for session {SessionId}", address, sessionId);
            throw;
        }
    }

    public async Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(
        string sessionId,
        string checkpointId)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return (new Dictionary<string, byte[]>(), Guid.NewGuid().ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get state for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<(bool success, string error, string newVersion)> SyncStateAsync(
        string sessionId,
        Dictionary<string, byte[]> stateUpdates,
        string version)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace with actual gRPC call once code is generated
            await Task.Delay(100);
            return (true, string.Empty, Guid.NewGuid().ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync state for session {SessionId}", sessionId);
            return (false, ex.Message, string.Empty);
        }
    }

    public void Dispose()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
        _channels.Clear();
    }
}