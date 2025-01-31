using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaMemoryScannerService : IMemoryScannerService
    {
        private readonly ILogger<FridaMemoryScannerService> _logger;
        private readonly IMemoryReaderService _memoryReader;
        private dynamic? _fridaScanner;

        public FridaMemoryScannerService(
            ILogger<FridaMemoryScannerService> logger,
            IMemoryReaderService memoryReader)
        {
            _logger = logger;
            _memoryReader = memoryReader;
            InitializePython();
        }

        private void InitializePython()
        {
            try
            {
                if (!PythonEngine.IsInitialized)
                {
                    Runtime.PythonDLL = @"python313.dll";
                    PythonEngine.Initialize();
                }

                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    string scriptPath = Path.GetDirectoryName(typeof(FridaMemoryService).Assembly.Location)!;
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

        public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
        {
            try
            {
                using (Py.GIL())
                {
                    if (_fridaScanner == null)
                        throw new InvalidOperationException("Frida scanner not initialized");

                    var process = System.Diagnostics.Process.GetProcessById(processId);
                    bool success = _fridaScanner.attach_to_process(process.ProcessName);
                    if (!success)
                    {
                        throw new Exception($"Failed to attach to process {process.ProcessName}");
                    }

                    // Convert pattern to string representation for Frida
                    var patternParts = new List<string>();
                    foreach (var b in pattern)
                    {
                        patternParts.Add(b.ToString("X2"));
                    }
                    var patternStr = string.Join(" ", patternParts);
                    var results = _fridaScanner.scan_memory("pattern", patternStr);

                    var addresses = new List<nint>();
                    foreach (string addr in results.As<string[]>())
                    {
                        addresses.Add((nint)Convert.ToInt64(addr));
                    }
                    return addresses;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pattern scan");
                throw;
            }
        }

        public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
        {
            try
            {
                using (Py.GIL())
                {
                    if (_fridaScanner == null)
                        throw new InvalidOperationException("Frida scanner not initialized");

                    var process = System.Diagnostics.Process.GetProcessById(processId);
                    bool success = _fridaScanner.attach_to_process(process.ProcessName);
                    if (!success)
                    {
                        throw new Exception($"Failed to attach to process {process.ProcessName}");
                    }

                    var results = _fridaScanner.scan_memory(valueType.ToString().ToLower(), value);

                    var addresses = new List<nint>();
                    foreach (string addr in results.As<string[]>())
                    {
                        addresses.Add((nint)Convert.ToInt64(addr));
                    }
                    return addresses;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during value scan");
                throw;
            }
        }

        public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
        {
            try
            {
                // For now, return an empty list as this is a specialized operation
                // that might need additional implementation in the Python script
                return new List<nint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all addresses");
                throw;
            }
        }
    }
}