using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services;

public class ScannerGrpcService : BaseGrpcService, IMemoryScannerService
{
    public ScannerGrpcService(
        ILogger<ScannerGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
    }

    public async Task<IEnumerable<string>> ScanAsync(ProcessInfo process, string searchPattern, int scanType, ScanProfile profile)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var request = new Proto.ScanRequest
            {
                SessionId = process.Id.ToString(),
                ValueType = "pattern",
                Value = ByteString.CopyFromUtf8(searchPattern),
                ScanType = scanType.ToString(),
                ComparisonType = profile.ComparisonType ?? "exact"
            };

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            return response.Results.Select(r => r.Address.ToString("X"));
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
            var client = CreateClient(channel);

            var request = new Proto.PatternScanRequest
            {
                SessionId = processId.ToString(),
                Pattern = ByteString.CopyFrom(pattern),
                Mask = mask
            };

            var response = await client.ScanPatternAsync(request, CreateMetadata());
            return response.Results.Select(r => new nint((long)r.Address)).ToList();
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
            var client = CreateClient(channel);

            var request = new Proto.ScanRequest
            {
                SessionId = processId.ToString(),
                ValueType = valueType.ToString().ToLowerInvariant(),
                Value = ByteString.CopyFrom(BitConverter.GetBytes(value)),
                ScanType = "exact"
            };

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            return response.Results.Select(r => new nint((long)r.Address)).ToList();
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
            var client = CreateClient(channel);

            var request = new Proto.ScanRequest
            {
                SessionId = processId.ToString(),
                ValueType = valueType.ToString().ToLowerInvariant(),
                ScanType = "all"
            };

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            return response.Results.Select(r => new nint((long)r.Address)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all addresses for process {ProcessId}", processId);
            throw;
        }
    }
}