using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BlazorFridaApp.MemoryScanner.Configuration;

namespace BlazorFridaApp.MemoryScanner.Services;

public class MemoryScannerGrpcService : IMemoryScannerGrpcService, IDisposable
{
    private readonly IProcessGrpcService _processService;
    private readonly IMemoryGrpcService _memoryService;
    private readonly IScannerGrpcService _scannerService;
    private readonly IStateGrpcService _stateService;
    private readonly IFreezeGrpcService _freezeService;
    private readonly ILogger<MemoryScannerGrpcService> _logger;
    private readonly MemoryScannerSettings _settings;
    private bool _disposed;

    public MemoryScannerGrpcService(
        IProcessGrpcService processService,
        IMemoryGrpcService memoryService,
        IScannerGrpcService scannerService,
        IStateGrpcService stateService,
        IFreezeGrpcService freezeService,
        IOptions<MemoryScannerSettings> settings,
        ILogger<MemoryScannerGrpcService> logger)
    {
        _processService = processService;
        _memoryService = memoryService;
        _scannerService = scannerService;
        _stateService = stateService;
        _freezeService = freezeService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<ProcessInfo>> ListProcessesAsync()
    {
        ThrowIfDisposed();
        var processes = await _processService.GetAccessibleProcessesAsync();
        return processes;
    }

    public async Task<(bool success, string sessionId)> AttachToProcessAsync(int pid)
    {
        ThrowIfDisposed();
        try
        {
            return await _processService.AttachToProcessAsync(pid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to process {ProcessId}", pid);
            return (false, string.Empty);
        }
    }

    public Task DetachFromProcessAsync(string sessionId)
    {
        ThrowIfDisposed();
        return _processService.DetachFromProcessAsync(sessionId);
    }

    public async Task<IEnumerable<ScanResult>> ScanMemoryAsync(
        string sessionId,
        string valueType,
        byte[] value,
        string comparisonType,
        IEnumerable<(ulong start, ulong end)> ranges)
    {
        ThrowIfDisposed();
        
        // Add pattern validation
        if (valueType.Equals("pattern", StringComparison.OrdinalIgnoreCase))
        {
            if (value == null || value.Length == 0)
            {
                throw new ArgumentException("Pattern cannot be empty", nameof(value));
            }
            if (value.Length < 4) // Minimum pattern length requirement
            {
                throw new ArgumentException("Pattern must be at least 4 bytes long", nameof(value));
            }
            if (value.Length > 256) // Maximum pattern length
            {
                throw new ArgumentException("Pattern cannot be longer than 256 bytes", nameof(value));
            }
        }
        
        var results = await _scannerService.ScanAsync(sessionId, valueType, value, comparisonType, ranges);
        return results;
    }

    public async Task<(byte[] value, bool success, string error)> ReadMemoryAsync(
        string sessionId,
        ulong address,
        int size,
        string valueType)
    {
        ThrowIfDisposed();
        return await _memoryService.ReadMemoryBytes(sessionId, address, size);
    }

    public async Task<(bool success, string error)> WriteMemoryAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType)
    {
        ThrowIfDisposed();
        return await _memoryService.WriteMemoryBytes(sessionId, address, value);
    }

    public IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _freezeService.FreezeValueAsync(sessionId, address, value, valueType, cancellationToken);
    }

    public Task UnfreezeValueAsync(string sessionId, ulong address)
    {
        ThrowIfDisposed();
        return _freezeService.UnfreezeValueAsync(sessionId, address);
    }

    public Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(string sessionId, string checkpointId)
    {
        ThrowIfDisposed();
        return _stateService.GetStateAsync(sessionId, checkpointId);
    }

    public Task<(bool success, string error, string newVersion)> SyncStateAsync(
        string sessionId,
        Dictionary<string, byte[]> stateUpdates,
        string version)
    {
        ThrowIfDisposed();
        return _stateService.SyncStateAsync(sessionId, stateUpdates, version);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_processService is IDisposable processDisposable)
                {
                    processDisposable.Dispose();
                }
                if (_memoryService is IDisposable memoryDisposable)
                {
                    memoryDisposable.Dispose();
                }
                if (_scannerService is IDisposable scannerDisposable)
                {
                    scannerDisposable.Dispose();
                }
                if (_stateService is IDisposable stateDisposable)
                {
                    stateDisposable.Dispose();
                }
                if (_freezeService is IDisposable freezeDisposable)
                {
                    freezeDisposable.Dispose();
                }
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}