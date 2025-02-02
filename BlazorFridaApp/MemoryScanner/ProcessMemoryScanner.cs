using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Operations;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner
{
    public class ProcessMemoryScanner : IProcessMemoryScanner
    {
        private readonly ProcessOperations _processOps;
        private readonly MemoryScanOperations _scanOps;
        private readonly MemoryAccessOperations _memoryOps;
        private readonly ValueFreezeOperations _freezeOps;
        private bool _disposed;

        public ProcessMemoryScanner(
            IProcessService processService,
            IMemoryReaderService memoryReader,
            IMemoryScannerService scanner,
            IValueFreezerService freezer,
            IScanProfileService profileService,
            ILoggerFactory loggerFactory)
        {
            _processOps = new ProcessOperations(
                processService,
                profileService,
                loggerFactory.CreateLogger<ProcessOperations>());
                
            _scanOps = new MemoryScanOperations(
                scanner,
                profileService,
                loggerFactory.CreateLogger<MemoryScanOperations>());
                
            _memoryOps = new MemoryAccessOperations(
                memoryReader,
                loggerFactory.CreateLogger<MemoryAccessOperations>());
                
            _freezeOps = new ValueFreezeOperations(
                freezer,
                loggerFactory.CreateLogger<ValueFreezeOperations>());
        }

        public List<ProcessInfo> GetProcesses() => _processOps.GetProcesses();

        public Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask) =>
            _scanOps.ScanForPattern(processId, pattern, mask);

        public Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType) =>
            _scanOps.ScanForValue(processId, value, valueType);

        public Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType) =>
            _scanOps.GetAllAddresses(processId, valueType);

        public Task<byte[]> ReadMemoryBytes(nint address, int length) =>
            _memoryOps.ReadMemoryBytes(address, length);

        public Task WriteMemory(nint address, byte[] value) =>
            _memoryOps.WriteMemory(address, value);

        public Task FreezeValue(nint address, byte[] value, string valueType) =>
            _freezeOps.FreezeValue(address, value, valueType);

        public Task UnfreezeValue(nint address) =>
            _freezeOps.UnfreezeValue(address);

        public Task SaveLastProcess(int processId) =>
            _processOps.SaveLastProcess(processId);

        public Task<int?> GetLastProcessId() =>
            _processOps.GetLastProcessId();

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _memoryOps.DisposeAsync();
                await _freezeOps.DisposeAsync();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}