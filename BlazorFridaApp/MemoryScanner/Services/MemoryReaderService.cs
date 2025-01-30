using System.Runtime.InteropServices;
using BlazorFridaApp.MemoryScanner.Native;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class MemoryReaderService : IMemoryReaderService
    {
        private readonly ILogger<MemoryReaderService> _logger;
        private nint _processHandle;
        private int _currentProcessId;
        private bool _disposed;

        public nint ProcessHandle => _processHandle;

        public MemoryReaderService(ILogger<MemoryReaderService> logger)
        {
            _logger = logger;
        }

        public void OpenProcess(int processId)
        {
            if (_processHandle != nint.Zero)
            {
                WindowsMemoryApi.CloseHandle(_processHandle);
            }
            
            _processHandle = WindowsMemoryApi.OpenProcess(
                WindowsMemoryApi.PROCESS_VM_READ | WindowsMemoryApi.PROCESS_QUERY_INFORMATION,
                false, processId);
            _currentProcessId = processId;
            
            if (_processHandle == nint.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to open process {ProcessId}. Error code: {ErrorCode}", processId, errorCode);
                throw new Exception($"Failed to open process (Error: {errorCode})");
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            return await Task.Run(() =>
            {
                var buffer = new byte[length];
                if (!WindowsMemoryApi.ReadProcessMemory(_processHandle, address, buffer, length, out _))
                {
                    throw new Exception($"Failed to read memory at {address:X} (Error: {Marshal.GetLastWin32Error()})");
                }
                return buffer;
            });
        }

        public async Task WriteMemoryBytes(nint address, byte[] value)
        {
            await Task.Run(() =>
            {
                if (_processHandle == nint.Zero)
                    throw new Exception("No process is currently open");

                // Try to write with existing handle first
                if (!WindowsMemoryApi.WriteProcessMemory(_processHandle, address, value, value.Length, out _))
                {
                    // If write fails, try to reopen handle with write access
                    var writeHandle = WindowsMemoryApi.OpenProcess(
                        WindowsMemoryApi.PROCESS_VM_WRITE | WindowsMemoryApi.PROCESS_VM_OPERATION,
                        false, _currentProcessId);
                    
                    if (writeHandle == nint.Zero)
                        throw new Exception($"Failed to open process for writing (Error: {Marshal.GetLastWin32Error()})");

                    try
                    {
                        if (!WindowsMemoryApi.WriteProcessMemory(writeHandle, address, value, value.Length, out _))
                            throw new Exception($"Write failed (Error: {Marshal.GetLastWin32Error()})");
                    }
                    finally
                    {
                        WindowsMemoryApi.CloseHandle(writeHandle);
                    }
                }
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_processHandle != nint.Zero)
                {
                    WindowsMemoryApi.CloseHandle(_processHandle);
                    _processHandle = nint.Zero;
                }
                _disposed = true;
            }
        }
    }
}