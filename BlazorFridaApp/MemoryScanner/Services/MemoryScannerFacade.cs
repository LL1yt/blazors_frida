using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.MemoryScanner.Services;

public class MemoryScannerFacade : IMemoryScannerGrpcService
{
    private readonly ProcessGrpcService _processService;
    private readonly MemoryGrpcService _memoryService;
    private readonly ScannerGrpcService _scannerService;
    private readonly StateGrpcService _stateService;
    private readonly FreezeGrpcService _freezeService;
    private readonly ILogger<MemoryScannerFacade> _logger;

    public MemoryScannerFacade(
        ProcessGrpcService processService,
        MemoryGrpcService memoryService,
        ScannerGrpcService scannerService,
        StateGrpcService stateService,
        FreezeGrpcService freezeService,
        ILogger<MemoryScannerFacade> logger)
    {
        _processService = processService;
        _memoryService = memoryService;
        _scannerService = scannerService;
        _stateService = stateService;
        _freezeService = freezeService;
        _logger = logger;
    }

    public Task<IEnumerable<Models.ProcessInfo>> ListProcessesAsync() =>
        _processService.GetAccessibleProcessesAsync();

    public Task<(bool success, string sessionId)> AttachToProcessAsync(int pid) =>
        _processService.AttachToProcessAsync(pid);

    public Task DetachFromProcessAsync(string sessionId) =>
        _processService.DetachFromProcessAsync(sessionId);

    public Task<IEnumerable<Models.ScanResult>> ScanMemoryAsync(
        string sessionId, 
        string valueType, 
        byte[] value, 
        string comparisonType, 
        IEnumerable<(ulong start, ulong end)> ranges) =>
        _scannerService.ScanAsync(
            new ProcessInfo { Id = int.Parse(sessionId.Split('-')[0]) },
            System.Text.Encoding.UTF8.GetString(value),
            0, // scanType будет определяться на основе valueType
            new ScanProfile { ComparisonType = comparisonType });

    public async Task<(byte[] value, bool success, string error)> ReadMemoryAsync(
        string sessionId,
        ulong address,
        int size,
        string valueType)
    {
        try
        {
            var result = await _memoryService.ReadMemoryBytes(new nint((long)address), size);
            return (result, true, string.Empty);
        }
        catch (Exception ex)
        {
            return (Array.Empty<byte>(), false, ex.Message);
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