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
            try
            {
                _fridaScanner = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    dynamic sys = Py.Import("sys");
                    string assemblyPath = Path.GetDirectoryName(typeof(FridaInteropService).Assembly.Location)!;
                    string nativePath = Path.Combine(assemblyPath, "MemoryScanner", "Native");
                    
                    if (!Directory.Exists(nativePath))
                    {
                        _logger.LogError($"Python modules directory not found: {nativePath}");
                        throw new FridaInteropException($"Python modules directory not found: {nativePath}");
                    }
                    
                    _logger.LogInformation($"Adding Python module path: {nativePath}");
                    sys.path.append(nativePath);

                    // Log the current Python path for debugging
                    _logger.LogInformation($"Python sys.path: {string.Join(", ", sys.path.ToString())}");

                    dynamic fridaModule;
                    try
                    {
                        fridaModule = Py.Import("frida_module");
                        _logger.LogInformation("Successfully imported frida_module");
                    }
                    catch (PythonException pex)
                    {
                        _logger.LogError(pex, "Failed to import frida_module. Python Error: {0}", pex.Message);
                        throw new FridaInteropException($"Failed to import frida_module: {pex.Message}", pex);
                    }
                    return fridaModule.FridaMemoryScanner();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Frida scanner");
                throw new FridaInteropException("Failed to initialize Frida scanner", ex);
            }
        }

        public bool AttachToProcess(string processName)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Attempting to attach to process: {ProcessName}", processName);
                EnsureInitialized();
                
                var result = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    try
                    {
                        return _fridaScanner!.attach_to_process(processName);
                    }
                    catch (PythonException pex)
                    {
                        _logger.LogError(pex, "Python error while attaching to process: {Message}", pex.Message);
                        throw new FridaInteropException($"Failed to attach to process: {pex.Message}", pex);
                    }
                });

                sw.Stop();
                _logger.LogInformation(
                    "Process attachment {Status} for {ProcessName} in {Duration}ms",
                    result ? "succeeded" : "failed", processName, sw.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex) when (ex is not FridaInteropException)
            {
                sw.Stop();
                _logger.LogError(ex,
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
                    _logger.LogInformation(
                        "Successfully read {ByteCount} bytes from {Address} in {Duration}ms",
                        result.Length, address, sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning(
                        "No data read from address {Address} in {Duration}ms",
                        address, sw.ElapsedMilliseconds);
                }
                
                return result;
            }
            catch (Exception ex) when (ex is not FridaInteropException)
            {
                sw.Stop();
                _logger.LogError(ex,
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
                _logger.LogDebug("Writing {ByteCount} bytes to address {Address}", value.Length, address);
                EnsureInitialized();
                
                var result = _pythonRuntime.ExecuteWithGIL(() =>
                {
                    try
                    {
                        return _fridaScanner!.write_memory(address, value);
                    }
                    catch (PythonException pex)
                    {
                        _logger.LogError(pex, "Python error while writing memory: {Message}", pex.Message);
                        throw new FridaInteropException($"Failed to write memory: {pex.Message}", pex);
                    }
                });

                sw.Stop();
                _logger.LogInformation(
                    "Memory write {Status} at {Address} in {Duration}ms",
                    result ? "succeeded" : "failed", address, sw.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex) when (ex is not FridaInteropException)
            {
                sw.Stop();
                _logger.LogError(ex,
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
                    Detach();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Frida cleanup");
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