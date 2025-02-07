using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using BlazorFridaApp.MemoryScanner.Models;

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
    private string _currentSessionId = string.Empty;
    private nint _processHandle;

    public nint ProcessHandle => _processHandle;

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

    protected virtual Proto.MemoryScanner.MemoryScannerClient CreateClient(GrpcChannel channel)
    {
        return new Proto.MemoryScanner.MemoryScannerClient(channel);
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
            var processes = await ListProcessesInternalAsync();
            return processes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list processes");
            throw;
        }
    }

    private async Task<List<ProcessInfo>> ListProcessesInternalAsync()
    {
        var processes = await GetAccessibleProcessesAsync();
        return processes;
    }

    public async Task<List<ProcessInfo>> GetAccessibleProcessesAsync()
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace placeholder with actual gRPC call
            return new List<ProcessInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get accessible processes");
            throw;
        }
    }

    public async Task<ProcessInfo> GetTargetProcessAsync()
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace placeholder with actual gRPC call
            return new ProcessInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get target process");
            throw;
        }
    }

    public async Task<(bool success, string sessionId)> AttachToProcessAsync(int pid)
    {
        try
        {
            var channel = await GetChannelAsync();
            _currentSessionId = Guid.NewGuid().ToString();
            OpenProcess(pid);
            return (true, _currentSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to process {Pid}", pid);
            return (false, string.Empty);
        }
    }

    public void OpenProcess(int processId)
    {
        // In the gRPC version, we don't actually need to open a process handle
        // as the Python server handles the process interaction
        _processHandle = new nint(processId);
    }

    public async Task DetachFromProcessAsync(string sessionId)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            
            var request = new Proto.DetachRequest
            {
                SessionId = sessionId
            };
            
            await client.DetachFromProcessAsync(request, CreateMetadata());
            
            if (sessionId == _currentSessionId)
            {
                _currentSessionId = string.Empty;
                _processHandle = 0;
            }
            
            _logger.LogInformation("Successfully detached from process for session {SessionId}", sessionId);
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
            var client = CreateClient(channel);

            var request = new Proto.ScanRequest
            {
                SessionId = sessionId,
                ValueType = valueType,
                Value = Google.Protobuf.ByteString.CopyFrom(value),
                ComparisonType = comparisonType,
                ScanType = "value" // Default scan type for value scanning
            };

            // Add memory ranges to scan
            foreach (var (start, end) in ranges)
            {
                request.Ranges.Add(new Proto.AddressRange
                {
                    Start = start,
                    End = end
                });
            }

            var response = await client.ScanMemoryAsync(request, CreateMetadata());

            if (response?.Results == null)
            {
                return Enumerable.Empty<Models.ScanResult>();
            }

            // Convert proto ScanResult to our Models.ScanResult
            return response.Results.Select(r => new Models.ScanResult
            {
                ProcessId = int.Parse(sessionId.Split('-')[0]), // Assuming session ID starts with process ID
                Addresses = new List<nint> { new nint((long)r.Address) },
                Pattern = r.Value.ToByteArray(),
                Mask = string.Empty // Not applicable for value scanning
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan memory for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<byte[]> ReadMemoryBytes(nint address, int length)
    {
        try
        {
            var result = await ReadMemoryAsync(_currentSessionId, (ulong)address, length, "bytes");
            return result.value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read memory bytes at {Address}", address);
            throw;
        }
    }

    public async Task WriteMemoryBytes(nint address, byte[] value)
    {
        try
        {
            await WriteMemoryAsync(_currentSessionId, (ulong)address, value, "bytes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write memory bytes at {Address}", address);
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
            var client = CreateClient(channel);

            var request = new Proto.ReadRequest
            {
                SessionId = sessionId,
                Address = address,
                Size = size,
                ValueType = valueType
            };

            var response = await client.ReadMemoryAsync(request, CreateMetadata());

            if (response == null)
            {
                return (System.Array.Empty<byte>(), false, "No response received from server");
            }

            return (response.Value.ToByteArray(), response.Success, response.ErrorMessage);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to read memory at {Address} for session {SessionId}", address, sessionId);
            return (System.Array.Empty<byte>(), false, ex.Status.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read memory at {Address} for session {SessionId}", address, sessionId);
            return (System.Array.Empty<byte>(), false, ex.Message);
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
            var client = CreateClient(channel);

            var request = new Proto.WriteRequest
            {
                SessionId = sessionId,
                Address = address,
                Value = Google.Protobuf.ByteString.CopyFrom(value),
                ValueType = valueType
            };

            var response = await client.WriteMemoryAsync(request, CreateMetadata());

            if (response == null)
            {
                return (false, "No response received from server");
            }

            return (response.Success, response.ErrorMessage);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to write memory at {Address} for session {SessionId}", address, sessionId);
            return (false, ex.Status.Detail);
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
        // TODO: Replace placeholder with actual gRPC streaming call
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
            // TODO: Replace placeholder with actual gRPC call
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
            var client = CreateClient(channel);

            var request = new Proto.StateRequest
            {
                SessionId = sessionId,
                CheckpointId = checkpointId ?? string.Empty
            };

            var response = await client.GetStateAsync(request, CreateMetadata());
            
            if (response == null)
            {
                return (new Dictionary<string, byte[]>(), string.Empty);
            }

            var state = response.State.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToByteArray()
            );

            return (state, response.Version);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to get state for session {SessionId} and checkpoint {CheckpointId}", sessionId, checkpointId);
            return (new Dictionary<string, byte[]>(), string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get state for session {SessionId} and checkpoint {CheckpointId}", sessionId, checkpointId);
            return (new Dictionary<string, byte[]>(), string.Empty);
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
            var client = CreateClient(channel);

            var request = new Proto.SyncRequest
            {
                SessionId = sessionId,
                Version = version ?? string.Empty
            };

            foreach (var kvp in stateUpdates)
            {
                request.StateUpdates[kvp.Key] = Google.Protobuf.ByteString.CopyFrom(kvp.Value);
            }

            var response = await client.SyncStateAsync(request, CreateMetadata());

            if (response == null)
            {
                return (false, "No response received from server", string.Empty);
            }

            return (response.Success, response.ErrorMessage, response.NewVersion);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to sync state for session {SessionId}", sessionId);
            return (false, ex.Status.Detail, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync state for session {SessionId}", sessionId);
            return (false, ex.Message, string.Empty);
        }
    }

    public async Task<IEnumerable<string>> ScanAsync(ProcessInfo process, string searchPattern, int scanType, ScanProfile profile)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace placeholder with actual gRPC call
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan process {ProcessId}", process.Id);
            throw;
        }
    }

    public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace placeholder with actual gRPC call
            return new List<nint>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan for pattern in process {ProcessId}", processId);
            throw;
        }
    }

    public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace placeholder with actual gRPC call
            return new List<nint>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan for value in process {ProcessId}", processId);
            throw;
        }
    }

    public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
    {
        try
        {
            var channel = await GetChannelAsync();
            // TODO: Replace placeholder with actual gRPC call
            return new List<nint>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all addresses for process {ProcessId}", processId);
            throw;
        }
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