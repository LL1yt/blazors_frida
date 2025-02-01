using System.Diagnostics;
using System.Text.Json;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaProcessService : IProcessService
    {
        private readonly ILogger<FridaProcessService> _logger;
        private dynamic? _fridaScanner;

        public FridaProcessService(ILogger<FridaProcessService> logger)
        {
            _logger = logger;
            InitializePython();
        }

        private void InitializePython()
        {
            try
            {
                // Initialize Python runtime if not already initialized
                if (!PythonEngine.IsInitialized)
                {
                    // Get Python home from environment
                    var pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME");
                    if (string.IsNullOrEmpty(pythonHome))
                    {
                        _logger.LogWarning("PYTHONHOME environment variable not set");
                        pythonHome = @"C:\Users\n0n4a\AppData\Local\Programs\Python\Python313"; // Default Python 3.13 installation path
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
                    // Import our Frida script
                    dynamic sys = Py.Import("sys");
                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MemoryScanner", "Native");
                    _logger.LogInformation($"Adding Python path: {scriptPath}");
                    sys.path.append(scriptPath);

                    try
                    {
                        // First try to import frida to check if it's available
                        dynamic frida = Py.Import("frida");
                        _logger.LogInformation("Successfully imported frida module");
                        
                        // Now import our custom module
                        dynamic fridaModule = Py.Import("frida_memory");
                        _logger.LogInformation("Successfully imported frida_memory module");
                        _fridaScanner = fridaModule.FridaMemoryScanner();
                    }
                    catch (PythonException pex)
                    {
                        Console.Error.WriteLine($"Python error during module import: {pex.Message}");
                        if (pex.Message.Contains("No module named"))
                        {
                            Console.Error.WriteLine($"Python path: {sys.path.ToString()}");
                            Console.Error.WriteLine("Please ensure frida is installed: pip install frida frida-tools");
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

        public List<Process> GetAccessibleProcesses()
        {
            try
            {
                using (Py.GIL())
                {
                    // Get process list from Frida
                    if (_fridaScanner == null)
                        throw new InvalidOperationException("Frida scanner not initialized");
                        
                    string jsonProcesses = _fridaScanner.get_process_list();
                    
                    // Parse JSON result
                    var processes = new List<Process>();
                    var processInfos = JsonSerializer.Deserialize<List<ProcessInfo>>(jsonProcesses);

                    if (processInfos != null)
                    {
                        foreach (var info in processInfos)
                        {
                            try
                            {
                                var process = Process.GetProcessById(info.Pid);
                                if (process != null && !string.IsNullOrEmpty(process.ProcessName) && !process.ProcessName.Equals("Idle", StringComparison.OrdinalIgnoreCase))
                                {
                                    processes.Add(process);
                                }
                            }
                            catch (ArgumentException)
                            {
                                // Process no longer exists, skip it
                                continue;
                            }
                        }
                    }

                    return processes;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get process list");
                return new List<Process>();
            }
        }

        private class ProcessInfo
        {
            public string Name { get; set; } = "";
            public int Pid { get; set; }
        }
    }
}