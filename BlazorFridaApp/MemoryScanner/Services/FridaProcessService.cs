using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text.Json;
using Python.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Security.Principal;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaProcessService : IProcessService, IDisposable
    {
        private readonly ILogger<FridaProcessService> _logger;
        private dynamic? _fridaScanner;
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public FridaProcessService(ILogger<FridaProcessService> logger)
        {
            _logger = logger;
        }

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            if (!await _initLock.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                _logger.LogError("Timeout waiting for initialization lock");
                throw new TimeoutException("Failed to acquire initialization lock");
            }

            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Starting Python/Frida initialization...");
                
                try
                {
                    if (!PythonEngine.IsInitialized)
                    {
                        _logger.LogDebug("Initializing Python engine...");
                        PythonEngine.Initialize();
                        _logger.LogInformation("Python engine initialized successfully");
                    }

                    using (Py.GIL())
                    {
                        _logger.LogDebug("Importing sys module...");
                        dynamic sys = Py.Import("sys");
                        _logger.LogDebug("sys.path: {Path}", (string)((dynamic)sys.path).ToString());

                        try
                        {
                            _logger.LogDebug("Attempting to import frida...");
                            dynamic frida = Py.Import("frida");
                            _logger.LogInformation("Successfully imported frida module");
                            
                            _logger.LogDebug("Attempting to import frida_module...");
                            dynamic fridaModule = Py.Import("frida_module");
                            _logger.LogInformation("Successfully imported frida_module module");

                            _logger.LogDebug("Creating FridaMemoryScanner instance...");
                            _fridaScanner = fridaModule.FridaMemoryScanner();
                            _logger.LogInformation("FridaMemoryScanner instance created successfully");
                            
                            _isInitialized = true;
                        }
                        catch (PythonException pex)
                        {
                            _logger.LogError(pex, "Python error during module import");
                            if (pex.Message.Contains("No module named"))
                            {
                                var pythonPath = (string)((dynamic)sys.path).ToString();
                                _logger.LogError("Python path: {Path}", pythonPath);
                                _logger.LogError("Please ensure frida is installed: pip install frida frida-tools");
                            }
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Python runtime");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initialization failed");
                _isInitialized = false;
                throw;
            }
            finally
            {
                _initLock.Release();
                _logger.LogInformation("Initialization completed. Success: {Success}", _isInitialized);
            }
        }

        public async Task<List<ProcessInfo>> GetAccessibleProcessesAsync()
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Starting to get accessible processes...");
            
            try
            {
                await EnsureInitializedAsync();

                List<ProcessInfo> processes = new();
                _logger.LogDebug("Getting process list");

                using (Py.GIL())
                {
                    if (_fridaScanner == null)
                    {
                        _logger.LogError("Frida scanner is null after initialization");
                        throw new InvalidOperationException("Frida scanner not initialized");
                    }

                    _logger.LogDebug("Calling Frida get_process_list");
                    string? jsonProcesses = null;
                    
                    // Use the same approach as in test
                    var done = new ManualResetEventSlim();
                    Exception? error = null;
                    
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            dynamic result = _fridaScanner.get_process_list();
                            jsonProcesses = (string)result;
                            _logger.LogDebug("Successfully got process list from Frida");
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                            _logger.LogError(ex, "Error getting process list from Frida");
                        }
                        finally
                        {
                            done.Set();
                        }
                    });
                    
                    thread.Start();
                    if (!done.Wait(TimeSpan.FromSeconds(5)))
                    {
                        throw new TimeoutException("Frida get_process_list call timed out");
                    }
                    
                    if (error != null)
                    {
                        throw error;
                    }

                    if (string.IsNullOrEmpty(jsonProcesses))
                    {
                        _logger.LogError("Frida returned empty process list");
                        return processes;
                    }

                    var processDatas = JsonSerializer.Deserialize<List<ProcessData>>(jsonProcesses);
                    if (processDatas == null || !processDatas.Any())
                    {
                        _logger.LogWarning("No processes found in Frida response");
                        return processes;
                    }

                    _logger.LogInformation("Found {Count} processes from frida", processDatas.Count);
                    var addedPids = new HashSet<int>();

                    foreach (var data in processDatas.Where(p => p.Pid != 0))
                    {
                        try
                        {
                            Process? process = null;
                            try { process = Process.GetProcessById(data.Pid); } catch { }
                            
                            if (process != null && !string.IsNullOrEmpty(process.ProcessName) && !addedPids.Contains(process.Id))
                            {
                                processes.Add(new ProcessInfo
                                {
                                    Id = process.Id,
                                    Name = process.ProcessName,
                                    DisplayName = $"{process.ProcessName} ({process.Id})"
                                });
                                addedPids.Add(process.Id);
                                _logger.LogTrace("Added process {Name} ({Id})", process.ProcessName, process.Id);
                            }
                            process?.Dispose();
                        }
                        catch (Exception ex) when (
                            ex is ArgumentException ||
                            ex is System.ComponentModel.Win32Exception ||
                            ex is InvalidOperationException)
                        {
                            _logger.LogWarning(ex, "Could not access process {Pid} ({Name})", data.Pid, data.Name);
                            continue;
                        }
                    }
                }
                
                sw.Stop();
                var orderedProcesses = processes.OrderBy(p => p.Name).ToList();
                _logger.LogInformation(
                    "Successfully retrieved {Count} accessible processes in {ElapsedMs}ms",
                    orderedProcesses.Count, sw.ElapsedMilliseconds);
                    
                return orderedProcesses;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Failed to get accessible processes after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                return new List<ProcessInfo>();
            }
        }

        public async Task<ProcessInfo> GetTargetProcessAsync()
        {
            var processes = await GetAccessibleProcessesAsync();
            return processes.FirstOrDefault() ?? throw new InvalidOperationException("No accessible process found");
        }

        public void Dispose()
        {
            if (_isInitialized && PythonEngine.IsInitialized)
            {
                try
                {
                    using (Py.GIL())
                    {
                        if (_fridaScanner != null)
                        {
                            _fridaScanner.Dispose();
                            _fridaScanner = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing FridaScanner");
                }
            }
            _initLock.Dispose();
        }

        private class ProcessData
        {
            public string Name { get; set; } = "";
            public int Pid { get; set; }
        }
    }
}
