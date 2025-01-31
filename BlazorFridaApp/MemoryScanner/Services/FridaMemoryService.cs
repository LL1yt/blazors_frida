using System.Runtime.InteropServices;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Python.Runtime;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaMemoryService : IMemoryReaderService
    {
        private readonly ILogger<FridaMemoryService> _logger;
        private dynamic? _fridaScanner;
        private bool _disposed;
        private string _processName = string.Empty;

        public nint ProcessHandle { get; private set; }

        public FridaMemoryService(ILogger<FridaMemoryService> logger)
        {
            _logger = logger;
            InitializePython();
        }

        private void InitializePython()
        {
            try
            {
                // Initialize Python runtime
                if (!PythonEngine.IsInitialized)
                {
                    Runtime.PythonDLL = @"python313.dll"; // Make sure this matches your Python version
                    PythonEngine.Initialize();
                }

                using (Py.GIL())
                {
                    // Import our Frida script
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

        public void OpenProcess(int processId)
        {
            try
            {
                using (Py.GIL())
                {
                    // Get process name from ID
                    var process = System.Diagnostics.Process.GetProcessById(processId);
                    _processName = process.ProcessName;

                    // Attach to process using Frida
                    if (_fridaScanner == null)
                        throw new InvalidOperationException("Frida scanner not initialized");
                        
                    bool success = _fridaScanner.attach_to_process(_processName);
                    if (!success)
                    {
                        throw new Exception($"Failed to attach to process {_processName}");
                    }

                    // Store process handle (just for compatibility, not actually used)
                    ProcessHandle = process.Handle;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to open process {processId}");
                throw;
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            try
            {
                using (Py.GIL())
                {
                    if (_fridaScanner == null)
                        throw new InvalidOperationException("Frida scanner not initialized");
                        
                    var result = _fridaScanner.read_memory(address.ToString(), length);
                    if (result == null)
                    {
                        throw new Exception($"Failed to read memory at address {address}");
                    }

                    // Convert Python list to byte array
                    return result.As<byte[]>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to read memory at address {address}");
                throw;
            }
        }

        public async Task WriteMemoryBytes(nint address, byte[] value)
        {
            try
            {
                using (Py.GIL())
                {
                    bool success = _fridaScanner.write_memory(address.ToString(), value);
                    if (!success)
                    {
                        throw new Exception($"Failed to write memory at address {address}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to write memory at address {address}");
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        using (Py.GIL())
                        {
                            if (_fridaScanner != null)
                            {
                                _fridaScanner.detach();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during Frida cleanup");
                    }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~FridaMemoryService()
        {
            Dispose(false);
        }
    }
}