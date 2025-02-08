using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Configuration;
using BlazorFridaApp.MemoryScanner.Operations;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BlazorFridaApp.MemoryScanner
{
    public sealed class ProcessMemoryScanner : IProcessMemoryScanner
    {
        private readonly ProcessOperations _processOps;
        private readonly MemoryScanOperations _scanOps;
        private readonly MemoryAccessOperations _memoryOps;
        private readonly ValueFreezeOperations _freezeOps;
        private readonly ILogger<ProcessMemoryScanner> _logger;
        private readonly SemaphoreSlim _operationLock;
        private readonly MemoryScannerSettings _settings;
        private readonly ConcurrentDictionary<int, ProcessMemoryInfo> _processMemoryInfo;
        private readonly Timer _memoryMonitorTimer;
        private bool _disposed;

        private class ProcessMemoryInfo
        {
            public long LastKnownMemoryUsage { get; set; }
            public DateTime LastAccessed { get; set; }
        }

        public ProcessMemoryScanner(
            IProcessService processService,
            IMemoryReaderService memoryReader,
            IMemoryScannerService scanner,
            IValueFreezerService freezer,
            IScanProfileService profileService,
            IOptions<MemoryScannerSettings> settings,
            ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(settings);

            _settings = settings.Value;
            _processMemoryInfo = new ConcurrentDictionary<int, ProcessMemoryInfo>();
            _operationLock = new SemaphoreSlim(1, 1);
            
            _processOps = new ProcessOperations(
                processService ?? throw new ArgumentNullException(nameof(processService)),
                profileService ?? throw new ArgumentNullException(nameof(profileService)),
                loggerFactory.CreateLogger<ProcessOperations>());
                
            _scanOps = new MemoryScanOperations(
                scanner ?? throw new ArgumentNullException(nameof(scanner)),
                profileService,
                loggerFactory.CreateLogger<MemoryScanOperations>());
                
            _memoryOps = new MemoryAccessOperations(
                memoryReader ?? throw new ArgumentNullException(nameof(memoryReader)),
                loggerFactory.CreateLogger<MemoryAccessOperations>());
                
            _freezeOps = new ValueFreezeOperations(
                freezer ?? throw new ArgumentNullException(nameof(freezer)),
                loggerFactory.CreateLogger<ValueFreezeOperations>());

            _logger = loggerFactory.CreateLogger<ProcessMemoryScanner>();
            
            _memoryMonitorTimer = new Timer(
                MonitorMemoryUsage,
                null,
                TimeSpan.FromMilliseconds(_settings.MemoryMonitorInterval),
                TimeSpan.FromMilliseconds(_settings.MemoryMonitorInterval));
        }

        private async void MonitorMemoryUsage(object? state)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var workingSet = currentProcess.WorkingSet64;
                var privateMemory = currentProcess.PrivateMemorySize64;

                _logger.LogTrace("Current memory usage - Working Set: {WorkingSet:N2} MB, Private Memory: {PrivateMemory:N2} MB",
                    workingSet / (1024.0 * 1024.0),
                    privateMemory / (1024.0 * 1024.0));

                // Check if we need to clean up old process info
                var oldProcesses = _processMemoryInfo
                    .Where(x => DateTime.UtcNow - x.Value.LastAccessed > TimeSpan.FromMinutes(30))
                    .Select(x => x.Key)
                    .ToList();

                foreach (var pid in oldProcesses)
                {
                    _processMemoryInfo.TryRemove(pid, out _);
                }

                // Check if any process is using too much memory
                foreach (var (pid, info) in _processMemoryInfo)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        info.LastKnownMemoryUsage = process.WorkingSet64;
                        
                        if (info.LastKnownMemoryUsage > _settings.MaxMemoryUsagePerProcess)
                        {
                            _logger.LogWarning("Process {ProcessId} memory usage ({Usage:N2} MB) exceeds limit ({Limit:N2} MB)",
                                pid,
                                info.LastKnownMemoryUsage / (1024.0 * 1024.0),
                                _settings.MaxMemoryUsagePerProcess / (1024.0 * 1024.0));
                            
                            await CleanupProcessResourcesAsync(pid).ConfigureAwait(false);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists
                        _processMemoryInfo.TryRemove(pid, out _);
                    }
                }

                // If our process is using too much memory, trigger cleanup
                if (privateMemory > _settings.MaxMemoryUsagePerProcess)
                {
                    _logger.LogWarning("Scanner process memory usage high ({Usage:N2} MB). Initiating cleanup...",
                        privateMemory / (1024.0 * 1024.0));
                    await ForceCleanupAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring memory usage");
            }
        }

        private async Task CleanupProcessResourcesAsync(int processId)
        {
            try
            {
                await _operationLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Cancel any ongoing operations
                    await _freezeOps.UnfreezeAll(processId).ConfigureAwait(false);
                    await _scanOps.ClearScanResults(processId).ConfigureAwait(false);
                }
                finally
                {
                    _operationLock.Release();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up resources for process {ProcessId}", processId);
            }
        }

        private async Task ForceCleanupAsync()
        {
            try
            {
                await _operationLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    foreach (var pid in _processMemoryInfo.Keys)
                    {
                        await CleanupProcessResourcesAsync(pid).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _operationLock.Release();
                }

                // Aggressive cleanup
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect(2, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forced cleanup");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ProcessMemoryScanner));
        }

        private void UpdateProcessAccess(int processId)
        {
            var info = _processMemoryInfo.GetOrAdd(processId, _ => new ProcessMemoryInfo());
            info.LastAccessed = DateTime.UtcNow;
        }

        public async Task<List<ProcessInfo>> GetProcessListAsync()
        {
            ThrowIfDisposed();
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var processes = await _processOps.GetProcessListAsync().ConfigureAwait(false);
                foreach (var process in processes)
                {
                    UpdateProcessAccess(process.Id);
                }
                return processes;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            ThrowIfDisposed();
            UpdateProcessAccess(processId);
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await _scanOps.ScanForPattern(processId, pattern, mask).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            ThrowIfDisposed();
            UpdateProcessAccess(processId);
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await _scanOps.ScanForValue(processId, value, valueType).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            ThrowIfDisposed();
            UpdateProcessAccess(processId);
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await _scanOps.GetAllAddresses(processId, valueType).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            ThrowIfDisposed();
            return await _memoryOps.ReadMemoryBytes(address, length).ConfigureAwait(false);
        }

        public async Task WriteMemory(nint address, byte[] value)
        {
            ThrowIfDisposed();
            await _memoryOps.WriteMemory(address, value).ConfigureAwait(false);
        }

        public async Task FreezeValue(nint address, byte[] value, string valueType)
        {
            ThrowIfDisposed();
            await _freezeOps.FreezeValue(address, value, valueType).ConfigureAwait(false);
        }

        public async Task UnfreezeValue(nint address)
        {
            ThrowIfDisposed();
            await _freezeOps.UnfreezeValue(address).ConfigureAwait(false);
        }

        public async Task SaveLastProcess(int processId)
        {
            ThrowIfDisposed();
            UpdateProcessAccess(processId);
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _processOps.SaveLastProcess(processId).ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<int?> GetLastProcessId()
        {
            ThrowIfDisposed();
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await _processOps.GetLastProcessId().ConfigureAwait(false);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await DisposeAsyncCore().ConfigureAwait(false);
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private async ValueTask DisposeAsyncCore()
        {
            try
            {
                _memoryMonitorTimer.Dispose();
                
                // Cancel all operations and cleanup
                await ForceCleanupAsync().ConfigureAwait(false);

                // Dispose operations in order of dependency
                await _freezeOps.DisposeAsync().ConfigureAwait(false);
                await _memoryOps.DisposeAsync().ConfigureAwait(false);
                
                if (_processOps is IAsyncDisposable processOpsDisposable)
                {
                    await processOpsDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (_processOps is IDisposable processOpsDisposableSync)
                {
                    processOpsDisposableSync.Dispose();
                }

                if (_scanOps is IAsyncDisposable scanOpsDisposable)
                {
                    await scanOpsDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (_scanOps is IDisposable scanOpsDisposableSync)
                {
                    scanOpsDisposableSync.Dispose();
                }
            }
            finally
            {
                _processMemoryInfo.Clear();
                _operationLock.Dispose();
            }
        }
    }
}