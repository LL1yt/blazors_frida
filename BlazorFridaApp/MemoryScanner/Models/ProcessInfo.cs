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
            var sw = Stopwatch.StartNew();
            try
            {
                _logger?.LogTrace("Starting conversion of Process {ProcessName} (ID: {ProcessId}) to ProcessInfo. HasExited: {HasExited}, Responding: {Responding}",
                    process.ProcessName, process.Id, process.HasExited, process.Responding);

                // Check if process is still valid
                if (process.HasExited)
                {
                    _logger?.LogWarning("Process {ProcessName} (ID: {ProcessId}) has exited during conversion",
                        process.ProcessName, process.Id);
                    throw new InvalidOperationException("Process has exited");
                }

                if (!process.Responding)
                {
                    _logger?.LogWarning("Process {ProcessName} (ID: {ProcessId}) is not responding during conversion",
                        process.ProcessName, process.Id);
                }

                var processInfo = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName
                };

                sw.Stop();
                _logger?.LogDebug(
                    "Successfully converted Process to ProcessInfo: {Name} (ID: {Id}) in {ElapsedMs}ms",
                    processInfo.Name, processInfo.Id, sw.ElapsedMilliseconds);

                return processInfo;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger?.LogError(ex,
                    "Failed to convert Process {ProcessName} (ID: {ProcessId}) to ProcessInfo after {ElapsedMs}ms. Error: {Error}",
                    process.ProcessName, process.Id, sw.ElapsedMilliseconds, ex.Message);
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