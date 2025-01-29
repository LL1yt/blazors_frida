using System.Diagnostics;
using System.Runtime.InteropServices;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.EntityFrameworkCore;
using BlazorFridaApp.Persistence;

namespace BlazorFridaApp.MemoryScanner
{
    public class ProcessMemoryScanner
    {
        private readonly AppDbContext _dbContext;
        public nint ProcessHandle => _processHandle;
        private nint _processHandle;
        private Timer? _freezeTimer;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress,
            [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress,
            byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        public ProcessMemoryScanner(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            const int PROCESS_VM_READ = 0x0010;
            const int PROCESS_QUERY_INFORMATION = 0x0400;
            
            _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, 
                false, processId);
            
            if (_processHandle == nint.Zero)
                throw new Exception($"Failed to open process (Error: {Marshal.GetLastWin32Error()})");

            var matches = new List<nint>();
            nint currentAddress = 0;
            const int bufferSize = 4096;
            var buffer = new byte[bufferSize];

            while (true)
            {
                if (!ReadProcessMemory(_processHandle, currentAddress, buffer, 
                    bufferSize, out var bytesRead) || bytesRead == 0)
                    break;

                for (int i = 0; i < bytesRead - pattern.Length; i++)
                {
                    if (PatternMatch(buffer, i, pattern, mask))
                    {
                        matches.Add(currentAddress + i);
                    }
                }

                currentAddress += (nint)bytesRead;
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
        }

        public void FreezeValue(nint address, byte[] value)
        {
            _freezeTimer?.Dispose();
            _freezeTimer = new Timer(async _ => 
            {
                await WriteMemory(address, value);
            }, null, 0, 1000);
        }

        public List<Process> GetProcesses()
        {
            return Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.ProcessName) && p.Id != 0)
                .OrderBy(p => p.ProcessName)
                .ToList();
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
            const int PROCESS_VM_WRITE = 0x0020;
            const int PROCESS_VM_OPERATION = 0x0008;
            
            var handle = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION, 
                false, Process.GetCurrentProcess().Id);
            
            if (handle == nint.Zero)
                throw new Exception($"Failed to open process for writing (Error: {Marshal.GetLastWin32Error()})");

            var success = WriteProcessMemory(handle, address, value, value.Length, out _);
            if (!success)
                throw new Exception($"Write failed (Error: {Marshal.GetLastWin32Error()})");

            await _dbContext.LockedAddresses.AddAsync(new LockedAddress
            {
                ProcessName = Process.GetCurrentProcess().ProcessName,
                Address = (long)address,
                ValueType = "byte[]",
                OriginalBytes = Array.Empty<byte>(),
                CurrentValue = value,
                IsFrozen = true,
                LastAccessed = DateTime.UtcNow
            });
            
            await _dbContext.SaveChangesAsync();
        }
    }
}