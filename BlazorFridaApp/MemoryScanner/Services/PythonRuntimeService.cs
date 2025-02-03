using Microsoft.Extensions.Logging;
using Python.Runtime;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class PythonRuntimeService : IPythonRuntimeService, IDisposable
    {
        private readonly ILogger<PythonRuntimeService> _logger;
        private bool _isInitialized;
        private readonly object _initLock = new object();

        public PythonRuntimeService(ILogger<PythonRuntimeService> logger)
        {
            _logger = logger;
        }

        public void EnsureInitialized()
        {
            if (_isInitialized) return;

            lock (_initLock)
            {
                if (_isInitialized) return;

                try
                {
                    LoggerExtensions.LogInformation(_logger, "Starting Python runtime initialization");
                    
                    if (!PythonEngine.IsInitialized)
                    {
                        LoggerExtensions.LogDebug(_logger, "Python runtime not initialized, starting initialization");
                        
                        // Log Python DLL path
                        var pythonDll = @"python313.dll";
                        LoggerExtensions.LogDebug(_logger, "Using Python DLL: {DllPath}", pythonDll);
                        Runtime.PythonDLL = pythonDll;
                        
                        // Check if DLL exists
                        var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pythonDll);
                        if (!File.Exists(dllPath))
                        {
                            LoggerExtensions.LogWarning(_logger, "Python DLL not found in application directory: {Path}", dllPath);
                            // Log system PATH for debugging
                            LoggerExtensions.LogDebug(_logger, "System PATH: {Path}", Environment.GetEnvironmentVariable("PATH"));
                        }
                        else
                        {
                            LoggerExtensions.LogDebug(_logger, "Python DLL found: {Path}", dllPath);
                        }
                        
                        LoggerExtensions.LogDebug(_logger, "Initializing Python engine");
                        PythonEngine.Initialize();
                        LoggerExtensions.LogInformation(_logger, "Python engine initialized successfully");
                        
                        // Log Python version and platform info
                        using (Py.GIL())
                        {
                            try
                            {
                                dynamic sys = Py.Import("sys");
                                LoggerExtensions.LogInformation(_logger, "Python version: {Version}", sys.version);
                                LoggerExtensions.LogDebug(_logger, "Python platform: {Platform}", sys.platform);
                            }
                            catch (Exception ex)
                            {
                                LoggerExtensions.LogWarning(_logger, ex, "Failed to get Python version info");
                            }
                        }
                    }
                    else
                    {
                        LoggerExtensions.LogDebug(_logger, "Python runtime already initialized");
                    }
                    
                    _isInitialized = true;
                    LoggerExtensions.LogInformation(_logger, "Python runtime initialization completed");
                }
                catch (Exception ex)
                {
                    LoggerExtensions.LogError(_logger, ex, "Failed to initialize Python runtime");
                    if (ex is DllNotFoundException dllEx)
                    {
                        LoggerExtensions.LogError(_logger, "Python DLL not found: {Message}", dllEx.Message);
                    }
                    throw new PythonRuntimeException("Failed to initialize Python runtime", ex);
                }
            }
        }

        public T ExecuteWithGIL<T>(Func<T> action)
        {
            EnsureInitialized();
            using (Py.GIL())
            {
                return action();
            }
        }

        public void ExecuteWithGIL(Action action)
        {
            EnsureInitialized();
            using (Py.GIL())
            {
                action();
            }
        }

        public void ReleaseGIL()
        {
            try
            {
                if (PythonEngine.IsInitialized)
                {
                    using (Py.GIL())
                    {
                        // Release the GIL properly
                        dynamic threading = Py.Import("threading");
                        threading.Lock().release();
                    }
                    _logger.LogDebug("Python GIL released successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing Python GIL");
                throw;
            }
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                try
                {
                    PythonEngine.Shutdown();
                }
                catch (Exception ex)
                {
                    LoggerExtensions.LogError(_logger, ex, "Error shutting down Python runtime");
                }
            }
        }
    }

    public class PythonRuntimeException : Exception
    {
        public PythonRuntimeException(string message) : base(message) { }
        public PythonRuntimeException(string message, Exception innerException) : base(message, innerException) { }
    }
}