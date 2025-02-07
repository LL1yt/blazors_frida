using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services;

public class MemoryGrpcService : BaseGrpcService, IMemoryReaderService
{
    private string _currentSessionId = string.Empty;
    private nint _processHandle;
    public nint ProcessHandle => _processHandle;

    public MemoryGrpcService(
        ILogger<MemoryGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
    }

    public void OpenProcess(int processId)
    {
        _processHandle = new nint(processId);
    }

    public async Task<byte[]> ReadMemoryBytes(nint address, int length)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            
            var request = new Proto.ReadRequest
            {
                SessionId = _currentSessionId,
                Address = (ulong)address,
                Size = length,
                ValueType = "bytes"
            };

            var response = await client.ReadMemoryAsync(request, CreateMetadata());
            
            if (!response.Success)
            {
                throw new InvalidOperationException(response.ErrorMessage);
            }

            return response.Value.ToByteArray();
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
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            
            var request = new Proto.WriteRequest
            {
                SessionId = _currentSessionId,
                Address = (ulong)address,
                Value = Google.Protobuf.ByteString.CopyFrom(value),
                ValueType = "bytes"
            };

            var response = await client.WriteMemoryAsync(request, CreateMetadata());
            
            if (!response.Success)
            {
                throw new InvalidOperationException(response.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write memory bytes at {Address}", address);
            throw;
        }
    }
}