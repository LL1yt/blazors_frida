using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public interface IFridaInteropService : IDisposable
    {
        void Initialize();
        bool AttachToProcess(string processName);
        byte[]? ReadMemory(string address, int length);
        bool WriteMemory(string address, byte[] value);
        void Detach();
    }

    public class FridaInteropService : IFridaInteropService, IDisposable
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
                    string scriptPath = Path.GetDirectoryName(typeof(FridaInteropService).Assembly.Location)!;
                    sys.path.append(scriptPath);

                    dynamic fridaModule = Py.Import("frida_module");
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
            EnsureInitialized();
            return _pythonRuntime.ExecuteWithGIL(() => _fridaScanner!.attach_to_process(processName));
        }

        public byte[]? ReadMemory(string address, int length)
        {
            EnsureInitialized();
            return _pythonRuntime.ExecuteWithGIL(() =>
            {
                var result = _fridaScanner!.read_memory(address, length);
                return result?.As<byte[]>();
            });
        }

        public bool WriteMemory(string address, byte[] value)
        {
            EnsureInitialized();
            return _pythonRuntime.ExecuteWithGIL(() => _fridaScanner!.write_memory(address, value));
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Detach();
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

        ~FridaInteropService()
        {
            Dispose(false);
        }
    }

    public class FridaInteropException : Exception
    {
        public FridaInteropException(string message) : base(message) { }
        public FridaInteropException(string message, Exception innerException) : base(message, innerException) { }
    }
}