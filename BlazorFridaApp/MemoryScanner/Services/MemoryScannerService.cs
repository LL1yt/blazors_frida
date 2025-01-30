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
            return Task.Run(() =>
            {
                _memoryReader.OpenProcess(processId);
                var matches = new List<nint>();
                var mbi = new WindowsMemoryApi.MEMORY_BASIC_INFORMATION();
                nint address = 0;

                while (WindowsMemoryApi.VirtualQueryEx(_memoryReader.ProcessHandle, address, out mbi,
                    Marshal.SizeOf<WindowsMemoryApi.MEMORY_BASIC_INFORMATION>()))
                {
                    // Check if memory region is committed and readable
                    if (mbi.State == 0x1000 && // MEM_COMMIT
                        (mbi.Protect & 0xF0) != 0x01) // Not PAGE_NOACCESS
                    {
                        var buffer = new byte[(int)mbi.RegionSize];
                        if (WindowsMemoryApi.ReadProcessMemory(_memoryReader.ProcessHandle, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead))
                        {
                            for (int i = 0; i < bytesRead - pattern.Length; i++)
                            {
                                if (PatternMatch(buffer, i, pattern, mask))
                                {
                                    matches.Add(mbi.BaseAddress + i);
                                }
                            }
                        }
                    }
                    
                    // Move to next region
                    address = mbi.BaseAddress + mbi.RegionSize;
                    
                    // Check if we've wrapped around memory space
                    if (address < mbi.BaseAddress)
                        break;
                }

                return matches;
            });
        }

        public Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            return Task.Run(async () =>
            {
                var bytes = valueType switch
                {
                    MemoryValueType.Int => BitConverter.GetBytes(value),
                    MemoryValueType.Float => BitConverter.GetBytes((float)value),
                    MemoryValueType.Double => BitConverter.GetBytes((double)value),
                    MemoryValueType.Short => BitConverter.GetBytes((short)value),
                    MemoryValueType.Long => BitConverter.GetBytes((long)value),
                    MemoryValueType.Byte => new[] { (byte)value },
                    _ => throw new ArgumentException($"Unsupported value type: {valueType}")
                };

                var mask = new string('x', bytes.Length);
                return await ScanForPattern(processId, bytes, mask);
            });
        }

        public Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            return Task.Run(() =>
            {
                _memoryReader.OpenProcess(processId);
                var matches = new List<nint>();
                var mbi = new WindowsMemoryApi.MEMORY_BASIC_INFORMATION();
                nint address = 0;

                int valueSize = valueType switch
                {
                    MemoryValueType.Byte => 1,
                    MemoryValueType.Short => 2,
                    MemoryValueType.Int => 4,
                    MemoryValueType.Float => 4,
                    MemoryValueType.Long => 8,
                    MemoryValueType.Double => 8,
                    _ => throw new ArgumentException($"Unsupported value type: {valueType}")
                };

                while (WindowsMemoryApi.VirtualQueryEx(_memoryReader.ProcessHandle, address, out mbi,
                    Marshal.SizeOf<WindowsMemoryApi.MEMORY_BASIC_INFORMATION>()))
                {
                    if (mbi.State == 0x1000 && (mbi.Protect & 0xF0) != 0x01)
                    {
                        var buffer = new byte[(int)mbi.RegionSize];
                        if (WindowsMemoryApi.ReadProcessMemory(_memoryReader.ProcessHandle, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead))
                        {
                            for (int i = 0; i <= bytesRead - valueSize; i += valueSize)
                            {
                                matches.Add(mbi.BaseAddress + i);
                            }
                        }
                    }
                    
                    address = mbi.BaseAddress + mbi.RegionSize;
                    if (address < mbi.BaseAddress)
                        break;
                }

                return matches;
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