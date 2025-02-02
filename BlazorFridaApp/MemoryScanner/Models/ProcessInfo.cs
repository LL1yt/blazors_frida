using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ProcessInfo
    {
        private static ILogger<ProcessInfo>? _logger;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName => $"{Name} ({Id})";

        public static void ConfigureLogger(ILogger<ProcessInfo> logger)
        {
            _logger = logger;
        }

        public static ProcessInfo FromProcess(Process process)
        {
            try
            {
                _logger?.LogTrace("Converting Process {ProcessName} (ID: {ProcessId}) to ProcessInfo",
                    process.ProcessName, process.Id);

                var processInfo = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName
                };

                _logger?.LogDebug("Successfully converted Process to ProcessInfo: {Name} (ID: {Id})",
                    processInfo.Name, processInfo.Id);

                return processInfo;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to convert Process {ProcessName} (ID: {ProcessId}) to ProcessInfo",
                    process.ProcessName, process.Id);
                throw;
            }
        }

        public Process ToProcess()
        {
            try
            {
                _logger?.LogTrace("Converting ProcessInfo {Name} (ID: {Id}) back to Process", Name, Id);
                var process = Process.GetProcessById(Id);
                _logger?.LogDebug("Successfully converted ProcessInfo back to Process");
                return process;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to convert ProcessInfo {Name} (ID: {Id}) back to Process",
                    Name, Id);
                throw;
            }
        }
    }
}