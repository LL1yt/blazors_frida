using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Native;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class ProcessService : IProcessService
    {
        private readonly ILogger<ProcessService> _logger;

        public ProcessService(ILogger<ProcessService> logger)
        {
            _logger = logger;
        }

        public List<Process> GetAccessibleProcesses()
        {
            try
            {
                _logger.LogInformation("Starting to enumerate system processes");
                
                var processes = new List<Process>();
                Process[] allProcesses;

                try
                {
                    allProcesses = Process.GetProcesses();
                    _logger.LogInformation("Found {Count} total processes", allProcesses.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enumerate system processes");
                    throw new InvalidOperationException("Failed to get system processes", ex);
                }

                int skippedCount = 0;
                int accessDeniedCount = 0;
                int exitedCount = 0;

                foreach (var p in allProcesses)
                {
                    if (string.IsNullOrEmpty(p.ProcessName) || p.Id == 0)
                    {
                        skippedCount++;
                        _logger.LogDebug("Skipping process with ID {ProcessId} (invalid name or ID)", p.Id);
                        continue;
                    }

                    try
                    {
                        if (p.HasExited)
                        {
                            exitedCount++;
                            _logger.LogDebug("Process {ProcessName} ({ProcessId}) has exited", p.ProcessName, p.Id);
                            continue;
                        }

                        _logger.LogDebug("Attempting to open process {ProcessName} ({ProcessId})", p.ProcessName, p.Id);
                        var handle = WindowsMemoryApi.OpenProcess(
                            WindowsMemoryApi.PROCESS_QUERY_INFORMATION | WindowsMemoryApi.PROCESS_VM_READ,
                            false,
                            p.Id);

                        if (handle == nint.Zero)
                        {
                            var error = Marshal.GetLastWin32Error();
                            accessDeniedCount++;
                            _logger.LogDebug("Access denied for process {ProcessName} ({ProcessId}). Error: {Error}",
                                p.ProcessName, p.Id, error);
                            continue;
                        }

                        try
                        {
                            processes.Add(p);
                            _logger.LogDebug("Successfully added process {ProcessName} ({ProcessId})", p.ProcessName, p.Id);
                        }
                        finally
                        {
                            _logger.LogTrace("Closing handle for process {ProcessName} ({ProcessId})", p.ProcessName, p.Id);
                            WindowsMemoryApi.CloseHandle(handle);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error accessing process {ProcessName} ({ProcessId})", p.ProcessName, p.Id);
                    }
                }

                var orderedProcesses = processes.OrderBy(p => p.ProcessName).ToList();
                
                _logger.LogInformation(
                    "Process enumeration completed: {AccessibleCount} accessible, {SkippedCount} skipped, " +
                    "{AccessDeniedCount} access denied, {ExitedCount} exited",
                    orderedProcesses.Count, skippedCount, accessDeniedCount, exitedCount);

                return orderedProcesses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during process enumeration");
                throw;
            }
        }
    }
}