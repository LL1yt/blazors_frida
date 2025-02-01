using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public interface IPythonRuntimeService
    {
        void EnsureInitialized();
        T ExecuteWithGIL<T>(Func<T> action);
        void ExecuteWithGIL(Action action);
    }

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
                    if (!PythonEngine.IsInitialized)
                    {
                        Runtime.PythonDLL = @"python313.dll";
                        PythonEngine.Initialize();
                    }
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Python runtime");
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
                    _logger.LogError(ex, "Error shutting down Python runtime");
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