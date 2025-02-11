using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;

namespace BlazorFridaApp.MemoryScanner.Services;

public sealed class ScannerGrpcService : BaseGrpcService, IScannerGrpcService
{
    private readonly ArrayPool<byte> _arrayPool;
    private readonly Dictionary<string, WeakReference<byte[]>> _resultCache;
    private readonly Dictionary<int, string> _sessionIds;  // Track process ID to session ID mapping
    private readonly object _cacheLock = new();
    private const int MaxCacheSize = 100;
    private const ulong DefaultMemoryStart = 0x00010000;  // Start of typical process memory
    private const ulong DefaultMemoryEnd = 0x7FFFFFFF;   // End of 32-bit address space
    private readonly Dictionary<string, List<nint>> _scanCache = new(); // Strong reference cache

    public ScannerGrpcService(
        ILogger<ScannerGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _arrayPool = ArrayPool<byte>.Shared;
        _resultCache = new Dictionary<string, WeakReference<byte[]>>();
        _sessionIds = new Dictionary<int, string>();
    }

    public async Task<(bool success, string sessionId)> AttachToProcessAsync(ProcessInfo process)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var request = new Proto.ProcessRequest
            {
                Pid = process.Id
            };

            var response = await client.AttachToProcessAsync(request, CreateMetadata());
            if (response.Success)
            {
                _sessionIds[process.Id] = response.SessionId;
            }
            return (response.Success, response.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to process {ProcessId}", process.Id);
            throw;
        }
    }

    private string GetSessionId(int processId)
    {
        if (!_sessionIds.TryGetValue(processId, out var sessionId))
        {
            throw new InvalidOperationException($"Process {processId} is not attached. Call AttachToProcessAsync first.");
        }
        return sessionId;
    }

    public async Task<IEnumerable<string>> ScanAsync(ProcessInfo process, string searchPattern, int scanType, ScanProfile profile)
    {
        try
        {
            var sessionId = GetSessionId(process.Id);
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var request = new Proto.ScanRequest
            {
                SessionId = sessionId,
                Value = ByteString.CopyFromUtf8(searchPattern),
                ScanType = scanType.ToString(),
                ValueType = profile.ValueType.ToString(),
                ComparisonType = profile.ComparisonType
            };

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            return response.Results.Select(r => r.Address.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan memory for process {ProcessId}", process.Id);
            throw;
        }
    }

    public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
    {
        try
        {
            if (pattern.Length != mask.Length)
            {
                throw new ArgumentException("Pattern and mask must have the same length");
            }

            var sessionId = GetSessionId(processId);
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var patternWithMask = string.Join(" ", pattern.Select((b, i) => mask[i] == 'x' ? b.ToString("X2") : "??"));
            var request = new Proto.PatternScanRequest
            {
                SessionId = sessionId,
                Pattern = patternWithMask
            };

            var response = await client.ScanPatternAsync(request, CreateMetadata());
            return response.Results.Select(r => new IntPtr((long)r.Address)).ToList();
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
            var sessionId = GetSessionId(processId);
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var request = new Proto.ScanRequest
            {
                SessionId = sessionId,
                Value = ByteString.CopyFrom(BitConverter.GetBytes(value)),
                ValueType = valueType.ToString(),
                ComparisonType = "exact",
                ScanType = "exact"
            };

            // Add default memory range
            request.Ranges.Add(new Proto.AddressRange
            {
                Start = DefaultMemoryStart,
                End = DefaultMemoryEnd
            });

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            var results = response.Results.Select(r => new IntPtr((long)r.Address)).ToList();
            _scanCache[sessionId] = results; // Strong reference cache
            return results;
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
            var sessionId = GetSessionId(processId);
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var request = new Proto.ScanRequest
            {
                SessionId = sessionId,
                ValueType = valueType.ToString(),
                ComparisonType = "all",
                ScanType = "all"
            };

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            return response.Results.Select(r => new IntPtr((long)r.Address)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all addresses for process {ProcessId}", processId);
            throw;
        }
    }

    public async Task<IEnumerable<ScanResult>> ScanAsync(
        string sessionId,
        string valueType,
        byte[] value,
        string comparisonType,
        IEnumerable<(ulong start, ulong end)> ranges)
    {
        try
        {
            if (valueType.Equals("pattern", StringComparison.OrdinalIgnoreCase))
            {
                if (value == null || value.Length == 0)
                {
                    throw new ArgumentException("Pattern cannot be empty", nameof(value));
                }

                if (value.Length < 2) // Minimum pattern length check
                {
                    throw new ArgumentException("Pattern must be at least 2 bytes long", nameof(value));
                }
            }

            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var request = new Proto.ScanRequest
            {
                SessionId = sessionId,
                Value = ByteString.CopyFrom(value),
                ValueType = valueType,
                ComparisonType = comparisonType,
                ScanType = comparisonType
            };

            foreach (var (start, end) in ranges)
            {
                request.Ranges.Add(new Proto.AddressRange
                {
                    Start = start,
                    End = end
                });
            }

            var response = await client.ScanMemoryAsync(request, CreateMetadata());
            return response.Results.Select(r => new ScanResult
            {
                Addresses = new List<nint> { new IntPtr((long)r.Address) }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan memory for session {SessionId}", sessionId);
            throw;
        }
    }

    protected override Proto.MemoryScanner.MemoryScannerClient CreateClient(GrpcChannel channel)
    {
        return new Proto.MemoryScanner.MemoryScannerClient(channel);
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

            if (_resultCache.Count > MaxCacheSize)
            {
                var excessCount = _resultCache.Count - MaxCacheSize;
                var oldestKeys = _resultCache.Keys.Take(excessCount).ToList();
                foreach (var key in oldestKeys)
                {
                    _resultCache.Remove(key);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var cacheItem in _resultCache.Values)
            {
                if (cacheItem.TryGetTarget(out var buffer))
                {
                    _arrayPool.Return(buffer);
                }
            }
            _resultCache.Clear();
            _scanCache.Clear();
        }
        base.Dispose(disposing);
    }
}