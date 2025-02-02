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
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Starting to get accessible processes...");
            
            try
            {
                _logger.LogDebug("Calling Process.GetProcesses()...");
                var processes = Process.GetProcesses();
                _logger.LogInformation("Found {Count} total processes in {ElapsedMs}ms",
                    processes.Length, sw.ElapsedMilliseconds);

                var result = new List<ProcessInfo>();
                var convertSw = Stopwatch.StartNew();
                var processedCount = 0;
                
                foreach (var process in processes)
                {
                    try
                    {
                        processedCount++;
                        if (processedCount % 100 == 0)
                        {
                            _logger.LogDebug("Processed {Count}/{Total} processes in {ElapsedMs}ms",
                                processedCount, processes.Length, convertSw.ElapsedMilliseconds);
                        }

                        _logger.LogTrace("Converting process {Name} (ID: {Id}, Responding: {Responding})",
                            process.ProcessName, process.Id, process.Responding);
                            
                        var processInfo = ProcessInfo.FromProcess(process);
                        result.Add(processInfo);
                        
                        process.Dispose(); // Clean up Process object
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to convert process {Name} (ID: {Id}). Error: {Error}",
                            process.ProcessName, process.Id, ex.Message);
                        process.Dispose(); // Ensure cleanup even on error
                    }
                }

                sw.Stop();
                _logger.LogInformation(
                    "Successfully retrieved {Count} accessible processes out of {Total} in {ElapsedMs}ms",
                    result.Count, processes.Length, sw.ElapsedMilliseconds);
                    
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Failed to get accessible processes after {ElapsedMs}ms. Error: {Error}",
                    sw.ElapsedMilliseconds, ex.Message);
                return new List<ProcessInfo>();
            }
        }
    }
}
