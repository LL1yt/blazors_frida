using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaProcessService : IProcessService
    {
        private readonly ILogger<FridaProcessService> _logger;

        public FridaProcessService(ILogger<FridaProcessService> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessInfo> GetTargetProcessAsync()
        {
            _logger.LogInformation("Getting target process...");
            try
            {
                var target = GetAccessibleProcesses().FirstOrDefault();
                if (target == null)
                {
                    _logger.LogWarning("No accessible process found");
                    throw new InvalidOperationException("No accessible process found.");
                }
                _logger.LogInformation("Found target process: {Name} (ID: {Id})",
                    target.Name, target.Id);
                return await Task.FromResult(target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get target process");
                throw;
            }
        }

        public IEnumerable<ProcessInfo> GetAccessibleProcesses()
        {
            _logger.LogInformation("Getting accessible processes...");
            try
            {
                var processes = Process.GetProcesses();
                _logger.LogDebug("Found {Count} total processes", processes.Length);

                var result = new List<ProcessInfo>();
                foreach (var process in processes)
                {
                    try
                    {
                        _logger.LogTrace("Converting process {Name} (ID: {Id})",
                            process.ProcessName, process.Id);
                        var processInfo = ProcessInfo.FromProcess(process);
                        result.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to convert process {Name} (ID: {Id})",
                            process.ProcessName, process.Id);
                    }
                }

                _logger.LogInformation("Successfully retrieved {Count} accessible processes", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get accessible processes");
                return new List<ProcessInfo>();
            }
        }
    }
}
