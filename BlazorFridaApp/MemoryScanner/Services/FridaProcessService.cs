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
                await Task.Run(() =>
                {
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
                                
                                _logger.LogDebug("Checking Frida device manager...");
                                try
                                {
                                    var deviceManagerTask = Task.Run(() => {
                                        dynamic deviceManager = frida.get_device_manager();
                                        dynamic localDevice = deviceManager.get_local_device();
                                        _logger.LogInformation("Frida local device: {Type}", (string)localDevice.type);
                                        return true;
                                    });

                                    if (!deviceManagerTask.Wait(TimeSpan.FromSeconds(5)))
                                    {
                                        throw new TimeoutException("Timeout while initializing Frida device manager");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to initialize Frida device manager");
                                    throw new InvalidOperationException("Frida device manager initialization failed", ex);
                                }
                                
                                _logger.LogDebug("Attempting to import frida_module...");
                                dynamic fridaModule = Py.Import("frida_module");
                                _logger.LogInformation("Successfully imported frida_module module");

                                _logger.LogDebug("Creating FridaMemoryScanner instance...");
                                var scannerTask = Task.Run(() => fridaModule.FridaMemoryScanner());
                                if (!scannerTask.Wait(TimeSpan.FromSeconds(5)))
                                {
                                    throw new TimeoutException("Timeout while creating FridaMemoryScanner instance");
                                }
                                _fridaScanner = scannerTask.Result;
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
                });
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
                // Check if running as admin
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                _logger.LogInformation("Running with admin privileges: {IsAdmin}", isAdmin);
                
                await EnsureInitializedAsync();

                using var cts = new CancellationTokenSource(OperationTimeout);
                return await Task.Run(() =>
                {
                    _logger.LogDebug("Entering Task.Run for process list retrieval");
                    List<ProcessInfo> processes = new();

                    try
                    {
                        _logger.LogDebug("Acquiring Python GIL");
                        var gilTimeout = Task.Delay(TimeSpan.FromSeconds(5));
                        var gilTask = Task.Run(() => {
                            using (Py.GIL())
                            {
                                if (_fridaScanner == null)
                                {
                                    _logger.LogError("Frida scanner is null after initialization");
                                    throw new InvalidOperationException("Frida scanner not initialized");
                                }

                                _logger.LogDebug("Calling Frida get_process_list");
                                dynamic? result = null;
                                try
                                {
                                    result = _fridaScanner.get_process_list();
                                    _logger.LogDebug("Successfully got process list from Frida");
                                }
                                catch (PythonException pex)
                                {
                                    _logger.LogError(pex, "Python error during get_process_list");
                                    if (pex.Message.Contains("timed out"))
                                    {
                                        throw new TimeoutException("Frida get_process_list call timed out", pex);
                                    }
                                    throw new InvalidOperationException("Failed to get process list from Frida", pex);
                                }
                                return result;
                            }
                        });

                        if (Task.WhenAny(gilTask, gilTimeout).Result == gilTimeout)
                        {
                            throw new TimeoutException("Timeout while acquiring GIL or calling Frida");
                        }

                        var result = gilTask.Result;
                        if (result == null)
                        {
                            _logger.LogError("Frida returned null process list");
                            throw new InvalidOperationException("Frida returned null process list");
                        }

                        string jsonProcesses = (string)result;
                        _logger.LogDebug("Got process list JSON: {Json}", jsonProcesses);

                        if (string.IsNullOrEmpty(jsonProcesses))
                        {
                            _logger.LogError("Frida returned empty process list");
                            throw new InvalidOperationException("Frida returned empty process list");
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
                            if (cts.Token.IsCancellationRequested)
                            {
                                _logger.LogWarning("Process enumeration cancelled");
                                break;
                            }

                            try
                            {
                                _logger.LogTrace("Processing PID: {Pid} ({Name})", data.Pid, data.Name);
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in Task.Run while getting process list");
                        throw;
                    }
                    
                    sw.Stop();
                    var orderedProcesses = processes.OrderBy(p => p.Name).ToList();
                    _logger.LogInformation(
                        "Successfully retrieved {Count} accessible processes in {ElapsedMs}ms",
                        orderedProcesses.Count, sw.ElapsedMilliseconds);
                        
                    return orderedProcesses;
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning("Process list retrieval timed out after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                return new List<ProcessInfo>();
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
