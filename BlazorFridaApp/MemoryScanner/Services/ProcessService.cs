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
            _logger.LogInformation("Getting list of processes...");
            
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

                    var handle = WindowsMemoryApi.OpenProcess(
                        WindowsMemoryApi.PROCESS_QUERY_INFORMATION | WindowsMemoryApi.PROCESS_VM_READ, 
                        false, 
                        p.Id);

                    if (handle == nint.Zero)
                    {
                        var error = Marshal.GetLastWin32Error();
                        _logger.LogDebug($"Cannot open process {p.ProcessName} ({p.Id}). Error: {error}");
                        continue;
                    }

                    WindowsMemoryApi.CloseHandle(handle);
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
    }
}