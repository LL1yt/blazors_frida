using System.Runtime.InteropServices;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Native;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class MemoryScannerService : IMemoryScannerService
    {
        private readonly IMemoryReaderService _memoryReader;
        private readonly ILogger<MemoryScannerService> _logger;

        public MemoryScannerService(
            IMemoryReaderService memoryReader,
            ILogger<MemoryScannerService> logger)
        {
            _memoryReader = memoryReader;
            _logger = logger;
        }

        public Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            if (processId <= 0)
                throw new ArgumentException("Invalid process ID", nameof(processId));
            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
            if (string.IsNullOrEmpty(mask) || mask.Length != pattern.Length)
                throw new ArgumentException("Mask must match pattern length", nameof(mask));

            return Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Starting pattern scan for process {ProcessId}", processId);
                    _memoryReader.OpenProcess(processId);
                    
                    if (_memoryReader.ProcessHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"Failed to open process {processId}");
                    }

                    var matches = new List<nint>();
                    var mbi = new WindowsMemoryApi.MEMORY_BASIC_INFORMATION();
                    nint address = 0;
                    int regionsScanned = 0;

                    while (WindowsMemoryApi.VirtualQueryEx(_memoryReader.ProcessHandle, address, out mbi,
                        Marshal.SizeOf<WindowsMemoryApi.MEMORY_BASIC_INFORMATION>()))
                    {
                        regionsScanned++;
                        _logger.LogDebug("Scanning memory region at {Address}, Size: {Size}",
                            mbi.BaseAddress, mbi.RegionSize);

                        // Check if memory region is committed and readable
                        if (mbi.State == 0x1000 && // MEM_COMMIT
                            (mbi.Protect & 0xF0) != 0x01) // Not PAGE_NOACCESS
                        {
                            try
                            {
                                var buffer = new byte[(int)mbi.RegionSize];
                                if (WindowsMemoryApi.ReadProcessMemory(_memoryReader.ProcessHandle, mbi.BaseAddress,
                                    buffer, buffer.Length, out var bytesRead))
                                {
                                    for (int i = 0; i < bytesRead - pattern.Length; i++)
                                    {
                                        if (PatternMatch(buffer, i, pattern, mask))
                                        {
                                            matches.Add(mbi.BaseAddress + i);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to read memory at address {Address}", mbi.BaseAddress);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error reading memory region at {Address}", mbi.BaseAddress);
                            }
                        }
                        
                        // Move to next region
                        address = mbi.BaseAddress + mbi.RegionSize;
                        
                        // Check if we've wrapped around memory space
                        if (address < mbi.BaseAddress)
                        {
                            _logger.LogDebug("Memory space wrap-around detected, stopping scan");
                            break;
                        }
                    }

                    _logger.LogInformation("Pattern scan completed. Scanned {RegionCount} regions, found {MatchCount} matches",
                        regionsScanned, matches.Count);
                    return matches;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Pattern scan failed for process {ProcessId}", processId);
                    throw;
                }
            });
        }

        public Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            if (processId <= 0)
                throw new ArgumentException("Invalid process ID", nameof(processId));

            return Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Starting value scan for process {ProcessId}, value {Value}, type {ValueType}",
                        processId, value, valueType);

                    byte[] bytes;
                    try
                    {
                        bytes = valueType switch
                        {
                            MemoryValueType.Int => BitConverter.GetBytes(value),
                            MemoryValueType.Float => BitConverter.GetBytes((float)value),
                            MemoryValueType.Double => BitConverter.GetBytes((double)value),
                            MemoryValueType.Short => BitConverter.GetBytes((short)value),
                            MemoryValueType.Long => BitConverter.GetBytes((long)value),
                            MemoryValueType.Byte => new[] { (byte)value },
                            _ => throw new ArgumentException($"Unsupported value type: {valueType}")
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error converting value {Value} to type {ValueType}", value, valueType);
                        throw;
                    }

                    var mask = new string('x', bytes.Length);
                    var results = await ScanForPattern(processId, bytes, mask);
                    _logger.LogInformation("Value scan completed. Found {ResultCount} matches", results.Count);
                    return results;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Value scan failed for process {ProcessId}", processId);
                    throw;
                }
            });
        }

        public Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            if (processId <= 0)
                throw new ArgumentException("Invalid process ID", nameof(processId));

            return Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Getting all addresses for process {ProcessId}, type {ValueType}",
                        processId, valueType);
                    
                    _memoryReader.OpenProcess(processId);
                    if (_memoryReader.ProcessHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"Failed to open process {processId}");
                    }

                    var matches = new List<nint>();
                    var mbi = new WindowsMemoryApi.MEMORY_BASIC_INFORMATION();
                    nint address = 0;
                    int regionsScanned = 0;

                    int valueSize;
                    try
                    {
                        valueSize = valueType switch
                        {
                            MemoryValueType.Byte => 1,
                            MemoryValueType.Short => 2,
                            MemoryValueType.Int => 4,
                            MemoryValueType.Float => 4,
                            MemoryValueType.Long => 8,
                            MemoryValueType.Double => 8,
                            _ => throw new ArgumentException($"Unsupported value type: {valueType}")
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Invalid value type specified: {ValueType}", valueType);
                        throw;
                    }

                    while (WindowsMemoryApi.VirtualQueryEx(_memoryReader.ProcessHandle, address, out mbi,
                        Marshal.SizeOf<WindowsMemoryApi.MEMORY_BASIC_INFORMATION>()))
                    {
                        regionsScanned++;
                        _logger.LogDebug("Scanning memory region at {Address}, Size: {Size}",
                            mbi.BaseAddress, mbi.RegionSize);

                        if (mbi.State == 0x1000 && (mbi.Protect & 0xF0) != 0x01)
                        {
                            try
                            {
                                var buffer = new byte[(int)mbi.RegionSize];
                                if (WindowsMemoryApi.ReadProcessMemory(_memoryReader.ProcessHandle, mbi.BaseAddress,
                                    buffer, buffer.Length, out var bytesRead))
                                {
                                    for (int i = 0; i <= bytesRead - valueSize; i += valueSize)
                                    {
                                        matches.Add(mbi.BaseAddress + i);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to read memory at address {Address}", mbi.BaseAddress);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error reading memory region at {Address}", mbi.BaseAddress);
                            }
                        }
                        
                        address = mbi.BaseAddress + mbi.RegionSize;
                        if (address < mbi.BaseAddress)
                        {
                            _logger.LogDebug("Memory space wrap-around detected, stopping scan");
                            break;
                        }
                    }

                    _logger.LogInformation("Address scan completed. Scanned {RegionCount} regions, found {MatchCount} addresses",
                        regionsScanned, matches.Count);
                    return matches;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Address scan failed for process {ProcessId}", processId);
                    throw;
                }
            });
        }

        private bool PatternMatch(byte[] buffer, int offset, byte[] pattern, string mask)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (mask[i] == 'x' && buffer[offset + i] != pattern[i])
                    return false;
            }
            return true;
        }
    }
}