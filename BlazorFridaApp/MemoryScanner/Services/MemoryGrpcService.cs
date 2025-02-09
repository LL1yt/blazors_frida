using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services;

public class MemoryGrpcService : BaseGrpcService, IMemoryGrpcService
{
    private string _currentSessionId = string.Empty;
    private nint _processHandle;
    public nint ProcessHandle => _processHandle;

    private readonly ILogger<MemoryGrpcService> _memoryLogger;

    public MemoryGrpcService(
        ILogger<MemoryGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _memoryLogger = logger;
    }

    public async Task<(byte[] value, bool success, string error)> ReadMemoryBytes(string sessionId, ulong address, int size)
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
                ValueType = "bytes"
            };
            var response = await client.ReadMemoryAsync(request, CreateMetadata());
            return (response.Value.ToByteArray(), response.Success, response.ErrorMessage);
        }
        catch (Exception ex)
        {
            _memoryLogger.LogError(ex, "Failed to read memory at address {Address}", address);
            return (Array.Empty<byte>(), false, ex.Message);
        }
    }

    public async Task<(bool success, string error)> WriteMemoryBytes(string sessionId, ulong address, byte[] value)
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
                ValueType = "bytes"
            };
            var response = await client.WriteMemoryAsync(request, CreateMetadata());
            return (response.Success, response.ErrorMessage);
        }
        catch (Exception ex)
        {
            _memoryLogger.LogError(ex, "Failed to write memory at address {Address}", address);
            return (false, ex.Message);
        }
    }

    // Legacy IMemoryReaderService implementation
    public void OpenProcess(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentException("Process ID must be a positive number", nameof(processId));
        }
        _processHandle = new nint(processId);
    }

    public async Task<byte[]> ReadMemoryBytes(nint address, int length)
    {
        var result = await ReadMemoryBytes(_currentSessionId, (ulong)address, length);
        if (!result.success)
        {
            throw new InvalidOperationException(result.error);
        }
        return result.value;
    }

    public async Task WriteMemoryBytes(nint address, byte[] value)
    {
        var result = await WriteMemoryBytes(_currentSessionId, (ulong)address, value);
        if (!result.success)
        {
            throw new InvalidOperationException(result.error);
        }
    }
}