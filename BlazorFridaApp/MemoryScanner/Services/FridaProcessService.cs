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

            try
            {
                await _initLock.WaitAsync();
                if (_isInitialized) return; // Double check after acquiring lock
                
                await Task.Run(() => {
                    try
                    {
                        if (!PythonEngine.IsInitialized)
                        {
                            var pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME");
                            if (string.IsNullOrEmpty(pythonHome))
                            {
                                _logger.LogWarning("PYTHONHOME environment variable not set");
                                pythonHome = @"C:\Users\n0n4a\AppData\Local\Programs\Python\Python313";
                            }
                            
                            Runtime.PythonDLL = Path.Combine(pythonHome, "python313.dll");
                            _logger.LogInformation($"Using Python DLL: {Runtime.PythonDLL}");
                            
                            if (!File.Exists(Runtime.PythonDLL))
                            {
                                throw new FileNotFoundException($"Python DLL not found at {Runtime.PythonDLL}");
                            }
                            
                            PythonEngine.Initialize();
                        }

                        using (Py.GIL())
                        {
                            dynamic sys = Py.Import("sys");
                            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MemoryScanner", "Native");
                            _logger.LogInformation($"Adding Python path: {scriptPath}");
                            sys.path.append(scriptPath);

                            try
                            {
                                dynamic frida = Py.Import("frida");
                                _logger.LogInformation("Successfully imported frida module");
                                
                                dynamic fridaModule = Py.Import("frida_module");
                                _logger.LogInformation("Successfully imported frida_module module");
                                _fridaScanner = fridaModule.FridaMemoryScanner();
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
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<List<ProcessInfo>> GetAccessibleProcessesAsync()
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Starting to get accessible processes...");
            
            try
            {
                await EnsureInitializedAsync();

                using var cts = new CancellationTokenSource(OperationTimeout);
                return await Task.Run(() =>
                {
                    using (Py.GIL())
                    {
                        if (_fridaScanner == null)
                            throw new InvalidOperationException("Frida scanner not initialized");

                        string jsonProcesses = _fridaScanner.get_process_list();
                        _logger.LogDebug("Got process list JSON: {Json}", jsonProcesses);

                        var processes = new List<ProcessInfo>();
                        var processDatas = JsonSerializer.Deserialize<List<ProcessData>>(jsonProcesses);
                        
                        if (processDatas != null)
                        {
                            _logger.LogInformation("Found {Count} processes from frida", processDatas.Count);
                            foreach (var data in processDatas)
                            {
                                try
                                {
                                    cts.Token.ThrowIfCancellationRequested();
                                    var process = Process.GetProcessById(data.Pid);
                                    if (process is not null)
                                    {
                                        if (!string.IsNullOrEmpty(process.ProcessName))
                                        {
                                            processes.Add(new ProcessInfo
                                            {
                                                Id = process.Id,
                                                Name = process.ProcessName,
                                                DisplayName = $"{process.ProcessName} ({process.Id})"
                                            });
                                        }
                                        process.Dispose();
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    _logger.LogWarning("Process enumeration timed out after {Timeout}ms", OperationTimeout.TotalMilliseconds);
                                    throw;
                                }
                                catch (Exception ex) when (
                                    ex is ArgumentException ||
                                    ex is System.ComponentModel.Win32Exception)
                                {
                                    _logger.LogWarning(ex, "Could not access process {Pid}", data.Pid);
                                    continue;
                                }
                            }
                        }
                        
                        sw.Stop();
                        _logger.LogInformation(
                            "Successfully retrieved {Count} accessible processes in {ElapsedMs}ms",
                            processes.Count, sw.ElapsedMilliseconds);
                            
                        return processes;
                    }
                }, cts.Token);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Failed to get accessible processes after {ElapsedMs}ms",
                    sw.ElapsedMilliseconds);
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
