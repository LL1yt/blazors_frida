using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace BlazorFridaApp.MemoryScanner.Services;

public sealed class ScannerGrpcService : BaseGrpcService, IMemoryScannerService
{
    private readonly ArrayPool<byte> _arrayPool;
    private readonly Dictionary<string, WeakReference<byte[]>> _resultCache;
    private readonly object _cacheLock = new();
    private const int MaxCacheSize = 100;

    public ScannerGrpcService(
        ILogger<ScannerGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _arrayPool = ArrayPool<byte>.Shared;
        _resultCache = new Dictionary<string, WeakReference<byte[]>>();
    }

    private void CleanupCache()
    {
        lock (_cacheLock)
        {
            var keysToRemove = _resultCache
                .Where(kvp => !kvp.Value.TryGetTarget(out _))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _resultCache.Remove(key);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GenerateCacheKey(int processId, byte[] pattern) =>
        $"{processId}_{Convert.ToBase64String(pattern)}";

    public async Task<IEnumerable<string>> ScanAsync(ProcessInfo process, string searchPattern, int scanType, ScanProfile profile)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var patternBytes = searchPattern.Split(' ')
                .Select(s => byte.Parse(s, System.Globalization.NumberStyles.HexNumber))
                .ToArray();

            var cacheKey = GenerateCacheKey(process.Id, patternBytes);
            byte[]? resultBuffer;

            // Try to get from cache first
            lock (_cacheLock)
            {
                if (_resultCache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out resultBuffer))
                {
                    _logger.LogDebug("Cache hit for scan pattern in process {ProcessId}", process.Id);
                    return ParseResults(resultBuffer);
                }
            }

            var request = new Proto.ScanRequest
            {
                SessionId = process.Id.ToString(),
                ValueType = "pattern",
                Value = ByteString.CopyFrom(patternBytes),
                ScanType = scanType.ToString(),
                ComparisonType = profile.ComparisonType ?? "exact"
            };

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            var results = response.Results.Select(r => r.Address.ToString("X")).ToList();

            // Cache the results
            if (results.Any())
            {
                resultBuffer = _arrayPool.Rent(results.Sum(r => r.Length + 1));
                try
                {
                    var offset = 0;
                    foreach (var result in results)
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(result);
                        Buffer.BlockCopy(bytes, 0, resultBuffer, offset, bytes.Length);
                        offset += bytes.Length + 1;
                    }

                    lock (_cacheLock)
                    {
                        if (_resultCache.Count >= MaxCacheSize)
                        {
                            CleanupCache();
                            if (_resultCache.Count >= MaxCacheSize)
                            {
                                // Remove oldest entry if still at capacity
                                _resultCache.Remove(_resultCache.Keys.First());
                            }
                        }
                        _resultCache[cacheKey] = new WeakReference<byte[]>(resultBuffer);
                    }
                }
                catch
                {
                    _arrayPool.Return(resultBuffer);
                    throw;
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory scan for process {ProcessId}", process.Id);
            throw;
        }
    }

    private IEnumerable<string> ParseResults(byte[] buffer)
    {
        var results = new List<string>();
        var start = 0;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 0 || i == buffer.Length - 1)
            {
                var length = i - start;
                if (length > 0)
                {
                    results.Add(System.Text.Encoding.UTF8.GetString(buffer, start, length));
                }
                start = i + 1;
            }
        }
        return results;
    }

    public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            // Use the dedicated pattern scan endpoint instead
            var request = new Proto.PatternScanRequest
            {
                SessionId = processId.ToString(),
                Pattern = Convert.ToHexString(pattern) + (string.IsNullOrEmpty(mask) ? "" : " " + mask)
            };

            var response = await client.ScanPatternAsync(request, CreateMetadata());
            return response.Results
                .Select(r => new nint((long)r.Address))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pattern scan for process {ProcessId}", processId);
            throw;
        }
    }

    public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var valueBytes = _arrayPool.Rent(sizeof(int));
            try
            {
                System.Buffer.BlockCopy(BitConverter.GetBytes(value), 0, valueBytes, 0, sizeof(int));

                var request = new Proto.ScanRequest
                {
                    SessionId = processId.ToString(),
                    ValueType = valueType.ToString().ToLowerInvariant(),
                    Value = ByteString.CopyFrom(valueBytes, 0, sizeof(int))
                };

                var response = await client.ScanMemoryAsync(request, CreateMetadata());
                return response.Results
                    .Select(r => new nint((long)r.Address))
                    .ToList();
            }
            finally
            {
                _arrayPool.Return(valueBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during value scan for process {ProcessId}", processId);
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
            return response.Results
                .Select(r => new nint((long)r.Address))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all addresses for process {ProcessId}", processId);
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_cacheLock)
            {
                _resultCache.Clear();
            }
        }
        base.Dispose(disposing);
    }
}