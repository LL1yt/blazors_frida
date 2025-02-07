using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces;

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