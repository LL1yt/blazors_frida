using BlazorFridaApp.MemoryScanner.Configuration;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace BlazorFridaApp.MemoryScanner.Services;

public class MemoryScannerFacade : IMemoryScannerGrpcService
{
    private readonly IProcessGrpcService _processService;
    private readonly IMemoryGrpcService _memoryService;
    private readonly IScannerGrpcService _scannerService;
    private readonly IStateGrpcService _stateService;
    private readonly IFreezeGrpcService _freezeService;
    private readonly ILogger<MemoryScannerFacade> _logger;
    private readonly MemoryScannerSettings _settings;

    public MemoryScannerFacade(
        IProcessGrpcService processService,
        IMemoryGrpcService memoryService,
        IScannerGrpcService scannerService,
        IStateGrpcService stateService,
        IFreezeGrpcService freezeService,
        IOptions<MemoryScannerSettings> settings,
        ILogger<MemoryScannerFacade> logger)
    {
        _processService = processService;
        _memoryService = memoryService;
        _scannerService = scannerService;
        _stateService = stateService;
        _freezeService = freezeService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<Models.ProcessInfo>> ListProcessesAsync()
    {
        var processes = await _processService.GetAccessibleProcessesAsync();
        return processes.AsEnumerable();
    }

    public Task<(bool success, string sessionId)> AttachToProcessAsync(int pid) =>
        _processService.AttachToProcessAsync(pid);

    public Task DetachFromProcessAsync(string sessionId) =>
        _processService.DetachFromProcessAsync(sessionId);

    public async Task<IEnumerable<ScanResult>> ScanMemoryAsync(
        string sessionId,
        string valueType,
        byte[] value,
        string comparisonType,
        IEnumerable<(ulong start, ulong end)> ranges)
    {
        try
        {
            var process = await _processService.GetTargetProcessAsync();
            if (process == null)
            {
                return Enumerable.Empty<ScanResult>();
            }

            if (!ranges.Any())
            {
                ranges = new[] { (_settings.DefaultMemoryRanges.DefaultStart, _settings.DefaultMemoryRanges.DefaultEnd) };
            }

            var scanProfile = new ScanProfile
            {
                ComparisonType = comparisonType ?? _settings.DefaultComparisonType
            };

            var addresses = await _scannerService.ScanAsync(process, sessionId, value.Length, scanProfile);
            return addresses.Select(addr => new ScanResult 
            { 
                Addresses = new List<nint> { new nint(Convert.ToInt64(addr, 16)) }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory scan");
            return Enumerable.Empty<ScanResult>();
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
            if (size <= 0 || size > _settings.MaxReadSize)
            {
                return (System.Array.Empty<byte>(), false, $"Invalid size. Must be between 1 and {_settings.MaxReadSize}");
            }

            var result = await _memoryService.ReadMemoryBytes((nint)address, size);
            return (result, true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading memory at address {Address}", address);
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
            await _memoryService.WriteMemoryBytes(new nint((long)address), value);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken) =>
        _freezeService.FreezeValueAsync(sessionId, address, value, valueType, cancellationToken);

    public Task UnfreezeValueAsync(string sessionId, ulong address) =>
        _freezeService.UnfreezeValueAsync(sessionId, address);

    public Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(
        string sessionId,
        string checkpointId) =>
        _stateService.GetStateAsync(sessionId, checkpointId);

    public Task<(bool success, string error, string newVersion)> SyncStateAsync(
        string sessionId,
        Dictionary<string, byte[]> stateUpdates,
        string version) =>
        _stateService.SyncStateAsync(sessionId, stateUpdates, version);
}