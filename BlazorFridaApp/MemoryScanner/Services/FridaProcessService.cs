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
                    Runtime.PythonDLL = @"python311.dll"; // Make sure this matches your Python version
                    PythonEngine.Initialize();
                }

                using (Py.GIL())
                {
                    // Import our Frida script
                    dynamic sys = Py.Import("sys");
                    string scriptPath = Path.GetDirectoryName(typeof(FridaProcessService).Assembly.Location)!;
                    sys.path.append(scriptPath);

                    dynamic fridaModule = Py.Import("frida_memory");
                    _fridaScanner = fridaModule.FridaMemoryScanner();
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
                                if (process != null && !string.IsNullOrEmpty(process.ProcessName))
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