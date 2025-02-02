using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;
using System.Diagnostics;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public interface IFridaInteropService : IAsyncDisposable
    {
        void Initialize();
        bool AttachToProcess(string processName);
        byte[]? ReadMemory(string address, int length);
        bool WriteMemory(string address, byte[] value);
        void Detach();
    }

    public class FridaInteropService : IFridaInteropService
    {
        private readonly ILogger<FridaInteropService> _logger;
        private readonly IPythonRuntimeService _pythonRuntime;
        private dynamic? _fridaScanner;
        private bool _disposed;

        public FridaInteropService(ILogger<FridaInteropService> logger, IPythonRuntimeService pythonRuntime)
        {
            _logger = logger;
            _pythonRuntime = pythonRuntime;
        }

        public void Initialize()
        {
            if (_disposed)
            {
                _disposed = false;
            }

            try
            {
                _fridaScanner = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    LoggerExtensions.LogInformation(_logger, "Starting Python initialization");
                    
                    dynamic sys = Py.Import("sys");
                    LoggerExtensions.LogDebug(_logger, "Successfully imported sys module");
                    
                    string assemblyPath = Path.GetDirectoryName(typeof(FridaInteropService).Assembly.Location)!;
                    string nativePath = Path.Combine(assemblyPath, "MemoryScanner", "Native");
                    
                    LoggerExtensions.LogDebug(_logger, "Assembly path: {Path}", assemblyPath);
                    LoggerExtensions.LogDebug(_logger, "Native modules path: {Path}", nativePath);
                    
                    if (!Directory.Exists(nativePath))
                    {
                        LoggerExtensions.LogError(_logger, "Python modules directory not found: {Path}", nativePath);
                        throw new FridaInteropException($"Python modules directory not found: {nativePath}");
                    }
                    
                    // Check if required Python files exist
                    var requiredFiles = new[] { "frida_module.py", "scanner.py", "reader.py", "writer.py" };
                    foreach (var file in requiredFiles)
                    {
                        var filePath = Path.Combine(nativePath, file);
                        if (!File.Exists(filePath))
                        {
                            LoggerExtensions.LogError(_logger, "Required Python file not found: {File}", filePath);
                            throw new FridaInteropException($"Required Python file not found: {filePath}");
                        }
                        LoggerExtensions.LogDebug(_logger, "Found required file: {File}", filePath);
                    }
                    
                    LoggerExtensions.LogInformation(_logger, "Adding Python module path: {Path}", nativePath);
                    sys.path.insert(0, nativePath);

                    // Log Python environment details
                    LoggerExtensions.LogDebug(_logger, "Python version: {Version}", sys.version);
                    LoggerExtensions.LogDebug(_logger, "Python executable: {Executable}", sys.executable);
                    LoggerExtensions.LogInformation(_logger, "Python sys.path: {Path}", string.Join(", ", sys.path.ToString()));

                    dynamic fridaModule;
                    try
                    {
                        LoggerExtensions.LogDebug(_logger, "Attempting to import frida_module");
                        fridaModule = Py.Import("frida_module");
                        LoggerExtensions.LogInformation(_logger, "Successfully imported frida_module");
                        
                        // Verify frida_module attributes
                        var attributes = fridaModule.GetAttr("__dict__").Keys();
                        LoggerExtensions.LogDebug(_logger, "frida_module attributes: {Attributes}", string.Join(", ", attributes));
                    }
                    catch (PythonException pex)
                    {
                        LoggerExtensions.LogError(_logger, pex, "Failed to import frida_module. Python Error: {Message}", pex.Message);
                        LoggerExtensions.LogDebug(_logger, "Python traceback: {Traceback}", pex.StackTrace);
                        throw new FridaInteropException($"Failed to import frida_module: {pex.Message}", pex);
                    }
                    return fridaModule.FridaMemoryScanner();
                });
            }
            catch (Exception ex)
            {
                LoggerExtensions.LogError(_logger, ex, "Failed to initialize Frida scanner");
                throw new FridaInteropException("Failed to initialize Frida scanner", ex);
            }
        }

        public bool AttachToProcess(string processName)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                LoggerExtensions.LogInformation(_logger, "Attempting to attach to process: {ProcessName}", processName);
                EnsureInitialized();
                
                var result = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    try
                    {
                        return _fridaScanner!.attach_to_process(processName);
                    }
                    catch (PythonException pex)
                    {
                        LoggerExtensions.LogError(_logger, pex, "Python error while attaching to process: {Message}", pex.Message);
                        throw new FridaInteropException($"Failed to attach to process: {pex.Message}", pex);
                    }
                });

                sw.Stop();
                LoggerExtensions.LogInformation(_logger,
                    "Process attachment {Status} for {ProcessName} in {Duration}ms",
                    result ? "succeeded" : "failed", processName, sw.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex) when (ex is not FridaInteropException)
            {
                sw.Stop();
                LoggerExtensions.LogError(_logger, ex,
                    "Unexpected error attaching to process {ProcessName}. Duration: {Duration}ms",
                    processName, sw.ElapsedMilliseconds);
                throw;
            }
        }

        public byte[]? ReadMemory(string address, int length)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                LoggerExtensions.LogInformation(_logger, "Reading memory at address {Address}, length: {Length}", address, length);
                EnsureInitialized();
                
                var result = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    try
                    {
                        var data = _fridaScanner!.read_memory(address, length);
                        return data?.As<byte[]>();
                    }
                    catch (PythonException pex)
                    {
                        _logger.LogError(pex, "Python error while reading memory: {Message}", pex.Message);
                        throw new FridaInteropException($"Failed to read memory: {pex.Message}", pex);
                    }
                });

                sw.Stop();
                if (result != null)
                {
                    LoggerExtensions.LogInformation(_logger,
                        "Successfully read {ByteCount} bytes from {Address} in {Duration}ms",
                        result.Length, address, sw.ElapsedMilliseconds);
                }
                else
                {
                    LoggerExtensions.LogWarning(_logger,
                        "No data read from address {Address} in {Duration}ms",
                        address, sw.ElapsedMilliseconds);
                }
                
                return result;
            }
            catch (Exception ex) when (ex is not FridaInteropException)
            {
                sw.Stop();
                LoggerExtensions.LogError(_logger, ex,
                    "Unexpected error reading memory at {Address}. Duration: {Duration}ms",
                    address, sw.ElapsedMilliseconds);
                throw;
            }
        }

        public bool WriteMemory(string address, byte[] value)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                LoggerExtensions.LogDebug(_logger, "Writing {ByteCount} bytes to address {Address}", value.Length, address);
                EnsureInitialized();
                
                var result = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    try
                    {
                        return _fridaScanner!.write_memory(address, value);
                    }
                    catch (PythonException pex)
                    {
                        LoggerExtensions.LogError(_logger, pex, "Python error while writing memory: {Message}", pex.Message);
                        throw new FridaInteropException($"Failed to write memory: {pex.Message}", pex);
                    }
                });

                sw.Stop();
                LoggerExtensions.LogInformation(_logger,
                    "Memory write {Status} at {Address} in {Duration}ms",
                    result ? "succeeded" : "failed", address, sw.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex) when (ex is not FridaInteropException)
            {
                sw.Stop();
                LoggerExtensions.LogError(_logger, ex,
                    "Unexpected error writing memory at {Address}. Duration: {Duration}ms",
                    address, sw.ElapsedMilliseconds);
                throw;
            }
        }

        public void Detach()
        {
            if (_fridaScanner != null)
            {
                _pythonRuntime.ExecuteWithGIL(() => _fridaScanner.detach());
            }
        }

        private void EnsureInitialized()
        {
            if (_fridaScanner == null)
            {
                throw new FridaInteropException("Frida scanner not initialized. Call Initialize() first.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    if (_fridaScanner != null)
                    {
                        _pythonRuntime.ExecuteWithGIL(() =>
                        {
                            try
                            {
                                _fridaScanner.detach();
                            }
                            catch (PythonException pex)
                            {
                                LoggerExtensions.LogError(_logger, pex, "Python error during cleanup: {Message}", pex.Message);
                            }
                        });
                        _fridaScanner = null;
                    }
                }
                catch (Exception ex)
                {
                    LoggerExtensions.LogError(_logger, ex, "Error during Frida cleanup");
                }
                
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }

    public class FridaInteropException : Exception
    {
        public FridaInteropException(string message) : base(message) { }
        public FridaInteropException(string message, Exception innerException) : base(message, innerException) { }
    }
}