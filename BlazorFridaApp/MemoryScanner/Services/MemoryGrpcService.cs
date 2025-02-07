using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;

namespace BlazorFridaApp.MemoryScanner.Services;

public class MemoryGrpcService : BaseGrpcService, IMemoryReaderService
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

    public void OpenProcess(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentException("Process ID must be a positive number", nameof(processId));
        }

        _memoryLogger.LogInformation("Opening process with ID: {ProcessId}", processId);
        
        try
        {
            _processHandle = new nint(processId);
            _memoryLogger.LogInformation("Successfully opened process with ID: {ProcessId}", processId);
        }
        catch (Exception ex)
        {
            _memoryLogger.LogError(ex, "Failed to open process with ID: {ProcessId}", processId);
            throw;
        }
    }

    public async Task<byte[]> ReadMemoryBytes(nint address, int length)
    {
        if (address == nint.Zero)
        {
            throw new ArgumentException("Memory address cannot be zero", nameof(address));
        }
        
        if (length <= 0)
        {
            throw new ArgumentException("Length must be a positive number", nameof(length));
        }

        if (_processHandle == nint.Zero)
        {
            throw new InvalidOperationException("Process handle is not initialized. Call OpenProcess first.");
        }

        _memoryLogger.LogInformation("Reading {Size} bytes from process at address {Address}", length, address);
        
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

            _memoryLogger.LogDebug("Successfully read {Size} bytes from process", length);
            return response.Value.ToByteArray();
        }
        catch (Exception ex)
        {
            _memoryLogger.LogError(ex, "Failed to read memory from process at address {Address}", address);
            throw;
        }
    }

    public async Task WriteMemoryBytes(nint address, byte[] value)
    {
        _memoryLogger.LogInformation("Writing {Size} bytes to process at address {Address}", value.Length, address);
        
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

            _memoryLogger.LogDebug("Successfully wrote {Size} bytes to process", value.Length);
        }
        catch (Exception ex)
        {
            _memoryLogger.LogError(ex, "Failed to write memory to process at address {Address}", address);
            throw;
        }
    }
}