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
            if (processId <= 0)
                throw new ArgumentException("Invalid process ID", nameof(processId));

            try
            {
                _logger.LogInformation("Opening process {ProcessId}", processId);

                if (_processHandle != nint.Zero)
                {
                    _logger.LogDebug("Closing existing process handle");
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
                    throw new InvalidOperationException($"Failed to open process (Error: {errorCode})");
                }

                _logger.LogInformation("Successfully opened process {ProcessId}", processId);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Unexpected error opening process {ProcessId}", processId);
                throw;
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            if (address == nint.Zero)
                throw new ArgumentException("Invalid memory address", nameof(address));
            if (length <= 0)
                throw new ArgumentException("Length must be greater than 0", nameof(length));
            if (_processHandle == nint.Zero)
                throw new InvalidOperationException("No process is currently open");

            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Reading {Length} bytes from address {Address:X}", length, address);
                    var buffer = new byte[length];
                    
                    if (!WindowsMemoryApi.ReadProcessMemory(_processHandle, address, buffer, length, out var bytesRead))
                    {
                        var error = Marshal.GetLastWin32Error();
                        _logger.LogError("Failed to read memory at {Address:X}. Error code: {Error}", address, error);
                        throw new InvalidOperationException($"Failed to read memory at {address:X} (Error: {error})");
                    }

                    if (bytesRead < length)
                    {
                        _logger.LogWarning("Partial read at {Address:X}: requested {Length} bytes, read {BytesRead} bytes",
                            address, length, bytesRead);
                    }

                    _logger.LogDebug("Successfully read {BytesRead} bytes from address {Address:X}", bytesRead, address);
                    return buffer;
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    _logger.LogError(ex, "Unexpected error reading memory at address {Address:X}", address);
                    throw;
                }
            });
        }

        public async Task WriteMemoryBytes(nint address, byte[] value)
        {
            if (address == nint.Zero)
                throw new ArgumentException("Invalid memory address", nameof(address));
            if (value == null || value.Length == 0)
                throw new ArgumentException("Value cannot be null or empty", nameof(value));
            if (_processHandle == nint.Zero)
                throw new InvalidOperationException("No process is currently open");

            await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Writing {Length} bytes to address {Address:X}", value.Length, address);

                    // Try to write with existing handle first
                    if (!WindowsMemoryApi.WriteProcessMemory(_processHandle, address, value, value.Length, out var bytesWritten))
                    {
                        _logger.LogDebug("Write failed with read handle, attempting to open write handle");
                        
                        // If write fails, try to reopen handle with write access
                        var writeHandle = WindowsMemoryApi.OpenProcess(
                            WindowsMemoryApi.PROCESS_VM_WRITE | WindowsMemoryApi.PROCESS_VM_OPERATION,
                            false, _currentProcessId);
                        
                        if (writeHandle == nint.Zero)
                        {
                            var error = Marshal.GetLastWin32Error();
                            _logger.LogError("Failed to open process for writing. Error code: {Error}", error);
                            throw new InvalidOperationException($"Failed to open process for writing (Error: {error})");
                        }

                        try
                        {
                            if (!WindowsMemoryApi.WriteProcessMemory(writeHandle, address, value, value.Length, out bytesWritten))
                            {
                                var error = Marshal.GetLastWin32Error();
                                _logger.LogError("Write failed with write handle. Error code: {Error}", error);
                                throw new InvalidOperationException($"Write failed (Error: {error})");
                            }
                        }
                        finally
                        {
                            _logger.LogDebug("Closing write handle");
                            WindowsMemoryApi.CloseHandle(writeHandle);
                        }
                    }

                    if (bytesWritten < value.Length)
                    {
                        _logger.LogWarning("Partial write at {Address:X}: attempted {Length} bytes, wrote {BytesWritten} bytes",
                            address, value.Length, bytesWritten);
                    }

                    _logger.LogInformation("Successfully wrote {BytesWritten} bytes to address {Address:X}",
                        bytesWritten, address);
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    _logger.LogError(ex, "Unexpected error writing memory at address {Address:X}", address);
                    throw;
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