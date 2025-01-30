using System.Diagnostics;
using System.Runtime.InteropServices;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.EntityFrameworkCore;
using BlazorFridaApp.Persistence;

namespace BlazorFridaApp.MemoryScanner
{
    public class ProcessMemoryScanner : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ProcessMemoryScanner> _logger;
        public nint ProcessHandle => _processHandle;
        private nint _processHandle;
        private int _currentProcessId;
        private readonly Dictionary<nint, Timer> _freezeTimers = new();
        private bool _disposed;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress,
            [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress,
            byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualQueryEx(nint hProcess, nint lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(nint hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public nint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public ProcessMemoryScanner(AppDbContext dbContext, ILogger<ProcessMemoryScanner> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _logger.LogInformation("ProcessMemoryScanner initialized");
        }

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            const int PROCESS_VM_READ = 0x0010;
            const int PROCESS_QUERY_INFORMATION = 0x0400;
            
            if (_processHandle != nint.Zero)
            {
                CloseHandle(_processHandle);
            }
            
            _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION,
                false, processId);
            _currentProcessId = processId;
            
            if (_processHandle == nint.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to open process {ProcessId}. Error code: {ErrorCode}", processId, errorCode);
                throw new Exception($"Failed to open process (Error: {errorCode})");
            }

            var matches = new List<nint>();
            var mbi = new MEMORY_BASIC_INFORMATION();
            nint address = 0;

            while (VirtualQueryEx(_processHandle, address, out mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
            {
                // Check if memory region is committed and readable
                if (mbi.State == 0x1000 && // MEM_COMMIT
                    (mbi.Protect & 0xF0) != 0x01) // Not PAGE_NOACCESS
                {
                    var buffer = new byte[(int)mbi.RegionSize];
                    if (ReadProcessMemory(_processHandle, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead))
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

            await SaveScanResults(processId, pattern, mask, matches);
            return matches;
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

        private async Task SaveScanResults(int processId, byte[] pattern, 
            string mask, List<nint> addresses)
        {
            var processName = Process.GetProcessById(processId).ProcessName;
            var profile = new ScanProfile
            {
                Name = $"{processName} Scan {DateTime.Now:yyyyMMdd-HHmmss}",
                ProcessName = processName,
                Pattern = pattern,
                Mask = mask,
                Offsets = addresses.Any() ? 
                    addresses.Select(a => (int)(a - addresses.First())).ToArray() : Array.Empty<int>(),
                Created = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            await _dbContext.ScanProfiles.AddAsync(profile);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Saved scan results for process {ProcessId} with {MatchCount} matches", processId, addresses.Count);
        }

        public async Task FreezeValue(nint address, byte[] value, string valueType)
        {
            if (_freezeTimers.TryGetValue(address, out var existingTimer))
            {
                existingTimer.Dispose();
                _freezeTimers.Remove(address);
            }

            var timer = new Timer(async _ =>
            {
                await WriteMemory(address, value);
            }, null, 0, 100); // Update every 100ms for more responsive freezing

            _freezeTimers[address] = timer;

            // Update or create locked address record
            var lockedAddress = await _dbContext.LockedAddresses
                .FirstOrDefaultAsync(la => la.Address == (long)address);

            if (lockedAddress == null)
            {
                lockedAddress = new LockedAddress
                {
                    ProcessName = Process.GetProcessById(_currentProcessId).ProcessName,
                    Address = (long)address,
                    ValueType = valueType,
                    OriginalBytes = await ReadMemoryBytes(address, value.Length),
                    CurrentValue = value,
                    IsFrozen = true,
                    LastAccessed = DateTime.UtcNow
                };
                await _dbContext.LockedAddresses.AddAsync(lockedAddress);
            }
            else
            {
                lockedAddress.CurrentValue = value;
                lockedAddress.ValueType = valueType;
                lockedAddress.IsFrozen = true;
                lockedAddress.LastAccessed = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UnfreezeValue(nint address)
        {
            if (_freezeTimers.TryGetValue(address, out var timer))
            {
                timer.Dispose();
                _freezeTimers.Remove(address);

                var lockedAddress = await _dbContext.LockedAddresses
                    .FirstOrDefaultAsync(la => la.Address == (long)address);

                if (lockedAddress != null)
                {
                    // Restore original value if available
                    if (lockedAddress.OriginalBytes.Length > 0)
                    {
                        await WriteMemory(address, lockedAddress.OriginalBytes);
                    }

                    lockedAddress.IsFrozen = false;
                    lockedAddress.LastAccessed = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            return await Task.Run(() =>
            {
                var buffer = new byte[length];
                if (!ReadProcessMemory(_processHandle, address, buffer, length, out _))
                {
                    throw new Exception($"Failed to read memory at {address:X} (Error: {Marshal.GetLastWin32Error()})");
                }
                return buffer;
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var timer in _freezeTimers.Values)
                {
                    timer.Dispose();
                }
                _freezeTimers.Clear();

                if (_processHandle != nint.Zero)
                {
                    CloseHandle(_processHandle);
                    _processHandle = nint.Zero;
                }

                _disposed = true;
            }
        }

        public List<Process> GetProcesses()
        {
            _logger.LogInformation("Getting list of processes...");
            
            const int PROCESS_QUERY_INFORMATION = 0x0400;
            const int PROCESS_VM_READ = 0x0010;

            var processes = new List<Process>();
            var allProcesses = Process.GetProcesses();
            
            _logger.LogInformation($"Found {allProcesses.Length} total processes");

            foreach (var p in allProcesses)
            {
                if (string.IsNullOrEmpty(p.ProcessName) || p.Id == 0)
                    continue;

                try
                {
                    if (p.HasExited)
                    {
                        _logger.LogDebug($"Process {p.ProcessName} ({p.Id}) has exited");
                        continue;
                    }

                    var handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, p.Id);
                    if (handle == nint.Zero)
                    {
                        var error = Marshal.GetLastWin32Error();
                        _logger.LogDebug($"Cannot open process {p.ProcessName} ({p.Id}). Error: {error}");
                        continue;
                    }

                    CloseHandle(handle);
                    processes.Add(p);
                    _logger.LogDebug($"Successfully added process {p.ProcessName} ({p.Id})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error accessing process {p.ProcessName} ({p.Id})");
                }
            }

            var orderedProcesses = processes.OrderBy(p => p.ProcessName).ToList();
            _logger.LogInformation($"Found {orderedProcesses.Count} accessible processes");
            return orderedProcesses;
        }

        public async Task SaveLastProcess(int processId)
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(a => a.Key == "LastProcess");
            
            if (setting == null)
            {
                setting = new ApplicationSetting { Key = "LastProcess" };
                await _dbContext.AddAsync(setting);
            }

            setting.Value = processId.ToString();
            setting.LastModified = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        public async Task<int?> GetLastProcessId()
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(a => a.Key == "LastProcess");
            
            if (setting != null && int.TryParse(setting.Value, out int processId))
            {
                return processId;
            }
            return null;
        }

        public async Task WriteMemory(nint address, byte[] value)
        {
            await Task.Run(() =>
            {
                if (_processHandle == nint.Zero)
                    throw new Exception("No process is currently open");

                const int PROCESS_VM_WRITE = 0x0020;
                const int PROCESS_VM_OPERATION = 0x0008;
                
                // Ensure we have write access
                if (!WriteProcessMemory(_processHandle, address, value, value.Length, out _))
                {
                    // If write fails, try to reopen handle with write access
                    var writeHandle = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
                        false, _currentProcessId);
                    
                    if (writeHandle == nint.Zero)
                        throw new Exception($"Failed to open process for writing (Error: {Marshal.GetLastWin32Error()})");

                    try
                    {
                        if (!WriteProcessMemory(writeHandle, address, value, value.Length, out _))
                            throw new Exception($"Write failed (Error: {Marshal.GetLastWin32Error()})");
                    }
                    finally
                    {
                        CloseHandle(writeHandle);
                    }
                }
            });
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
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
        }

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            const int PROCESS_VM_READ = 0x0010;
            const int PROCESS_QUERY_INFORMATION = 0x0400;
            
            if (_processHandle != nint.Zero)
            {
                CloseHandle(_processHandle);
            }
            
            _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION,
                false, processId);
            _currentProcessId = processId;
            
            if (_processHandle == nint.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to open process {ProcessId}. Error code: {ErrorCode}", processId, errorCode);
                throw new Exception($"Failed to open process (Error: {errorCode})");
            }

            var matches = new List<nint>();
            var mbi = new MEMORY_BASIC_INFORMATION();
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

            while (VirtualQueryEx(_processHandle, address, out mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
            {
                if (mbi.State == 0x1000 && (mbi.Protect & 0xF0) != 0x01)
                {
                    var buffer = new byte[(int)mbi.RegionSize];
                    if (ReadProcessMemory(_processHandle, mbi.BaseAddress, buffer, buffer.Length, out var bytesRead))
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
        }
    }
}