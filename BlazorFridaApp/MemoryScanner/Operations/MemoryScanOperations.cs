using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public interface IMemoryScanOperations : IAsyncDisposable
    {
        Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask);
        Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType);
        Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType);
        Task ClearScanResults(int processId);
    }

    public sealed class MemoryScanOperations : MemoryScannerBase, IMemoryScanOperations
    {
        private readonly IMemoryScannerService _scanner;
        private readonly IScanProfileService _profileService;
        private readonly ConcurrentDictionary<int, List<WeakReference<List<nint>>>> _scanResultCache;
        private readonly SemaphoreSlim _scanLock;
        private const int MaxConcurrentScans = 2;
        private const int MaxResultsPerProcess = 1000000;
        private bool _disposed;

        public MemoryScanOperations(
            IMemoryScannerService scanner,
            IScanProfileService profileService,
            ILogger<MemoryScanOperations> logger) : base(logger)
        {
            _scanner = scanner;
            _profileService = profileService;
            _scanResultCache = new ConcurrentDictionary<int, List<WeakReference<List<nint>>>>();
            _scanLock = new SemaphoreSlim(MaxConcurrentScans, MaxConcurrentScans);
        }

        private void CleanupStaleResults()
        {
            foreach (var processId in _scanResultCache.Keys)
            {
                if (_scanResultCache.TryGetValue(processId, out var resultRefs))
                {
                    resultRefs.RemoveAll(weakRef => !weakRef.TryGetTarget(out _));
                }
            }
        }

        public async Task ClearScanResults(int processId)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(MemoryScanOperations));

            await _scanLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_scanResultCache.TryRemove(processId, out var results))
                {
                    results.Clear();
                    _logger.LogInformation("Cleared scan results for process {ProcessId}", processId);
                }
            }
            finally
            {
                _scanLock.Release();
            }
        }

        private void CacheResults(int processId, List<nint> results)
        {
            if (!_scanResultCache.TryGetValue(processId, out var resultRefs))
            {
                resultRefs = new List<WeakReference<List<nint>>>();
                _scanResultCache[processId] = resultRefs;
            }

            resultRefs.Add(new WeakReference<List<nint>>(results));

            while (resultRefs.Count > 10)
            {
                CleanupStaleResults();
                if (resultRefs.Count > 10)
                {
                    resultRefs.RemoveAt(0);
                }
            }
        }

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(MemoryScanOperations));

            await _scanLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await ExecuteWithLogging(
                    async () =>
                    {
                        var matches = await _scanner.ScanForPattern(processId, pattern, mask);
                        if (matches.Count > MaxResultsPerProcess)
                        {
                            _logger.LogWarning("Pattern scan returned {Count} results, which exceeds the maximum of {Max}. Results will be truncated.",
                                matches.Count, MaxResultsPerProcess);
                            matches = matches.Take(MaxResultsPerProcess).ToList();
                        }

                        await _profileService.SaveScanResults(new[] { new ScanResult
                        {
                            ProcessId = processId,
                            Pattern = pattern,
                            Mask = mask,
                            Addresses = matches
                        }});

                        CacheResults(processId, matches);
                        return matches;
                    },
                    "Pattern scan",
                    processId);
            }
            finally
            {
                _scanLock.Release();
            }
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(MemoryScanOperations));

            await _scanLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await ExecuteWithLogging(
                    async () =>
                    {
                        var buffer = RentBuffer(Marshal.SizeOf<int>());
                        try
                        {
                            Marshal.WriteInt32(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0), value);
                            var matches = await _scanner.ScanForValue(processId, value, valueType);
                            
                            if (matches.Count > MaxResultsPerProcess)
                            {
                                _logger.LogWarning("Value scan returned {Count} results, which exceeds the maximum of {Max}. Results will be truncated.",
                                    matches.Count, MaxResultsPerProcess);
                                matches = matches.Take(MaxResultsPerProcess).ToList();
                            }

                            CacheResults(processId, matches);
                            return matches;
                        }
                        finally
                        {
                            ReturnBuffer(buffer);
                        }
                    },
                    "Value scan",
                    processId, value, valueType);
            }
            finally
            {
                _scanLock.Release();
            }
        }

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(MemoryScanOperations));

            await _scanLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await ExecuteWithLogging(
                    () => _scanner.GetAllAddresses(processId, valueType),
                    "Getting all addresses",
                    processId, valueType);
            }
            finally
            {
                _scanLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await DisposeAsyncCore().ConfigureAwait(false);
                Dispose(disposing: false);
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private ValueTask DisposeAsyncCore()
        {
            try
            {
                _scanResultCache.Clear();
                _scanLock.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing scan operations");
            }

            return ValueTask.CompletedTask;
        }

        protected override void OnDispose()
        {
            _scanResultCache.Clear();
            _scanLock.Dispose();
        }
    }
}