using System;
using System.Diagnostics;
using Python.Runtime;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace PythonNetTest;

public class TestPythonNet : IDisposable
{
    private readonly ILogger _logger;
    private bool _isInitialized;
    private bool _isDisposing;
    private dynamic? _testModule;
    private readonly ManualResetEventSlim _cleanupEvent;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public TestPythonNet(ILogger logger)
    {
        _logger = logger;
        _cleanupEvent = new ManualResetEventSlim(false);
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Set up Ctrl+C handler
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent the process from terminating immediately
            _logger.LogInformation("Ctrl+C received, initiating shutdown...");
            _cancellationTokenSource.Cancel();
            
            // Clean up Python resources
            try
            {
                if (_isInitialized && PythonEngine.IsInitialized)
                {
                    using (Py.GIL())
                    {
                        _testModule = null;
                    }
                    PythonEngine.Shutdown();
                    _isInitialized = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Python cleanup on Ctrl+C");
            }
            
            // Signal completion
            _cleanupEvent.Set();
            
            // Now we can exit
            Environment.Exit(0);
        };
    }

    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            _logger.LogInformation("Initializing Python engine...");
            
            // Set PythonHome and PythonDLL first
            var pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME") 
                ?? @"C:\Users\n0n4a\AppData\Local\Programs\Python\Python313";
            
            // Set PythonDLL before any engine initialization
            if (string.IsNullOrEmpty(Runtime.PythonDLL))
            {
                var pythonDll = Path.Combine(pythonHome, "python313.dll");
                _logger.LogInformation("Setting Python DLL: {DllPath}", pythonDll);
                Runtime.PythonDLL = pythonDll;
            }
            
            // Initialize only if not already initialized
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
                _isInitialized = true;
                _logger.LogInformation("Python initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Python");
            throw;
        }
    }

    private void CleanupCallback()
    {
        _logger.LogInformation("Cleanup callback from Python triggered");
        _cleanupEvent.Set();
    }

    public void RunTest()
    {
        try
        {
            _logger.LogInformation("Starting Python.NET test sequence...");

            // Run long running function test
            _logger.LogInformation("Press Enter to start long running function test...");
            if (Console.ReadLine() == null || _cancellationTokenSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RunSingleTest("Long running function test", () => {
                InitializePython();
                if (_testModule == null) throw new InvalidOperationException("Test module not initialized");
                return _testModule.run_long_running_test();
            });

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                return;
            }

            // Run GIL test
            _logger.LogInformation("Press Enter to start GIL test...");
            if (Console.ReadLine() == null || _cancellationTokenSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RunSingleTest("GIL test", () => {
                InitializePython();
                if (_testModule == null) throw new InvalidOperationException("Test module not initialized");
                return _testModule.run_gil_test();
            });

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                return;
            }

            // Run Frida test
            _logger.LogInformation("Press Enter to start Frida test...");
            if (Console.ReadLine() == null || _cancellationTokenSource.Token.IsCancellationRequested)
            {
                return;
            }
            
            RunSingleTest("Frida test", () => {
                InitializePython();
                if (_testModule == null) throw new InvalidOperationException("Test module not initialized");
                return _testModule.run_frida_test();
            });

            _logger.LogInformation("All tests completed successfully");
            _logger.LogInformation("Tests completed. Press Ctrl+C to exit...");
            
            // Wait for cancellation
            try
            {
                _cancellationTokenSource.Token.WaitHandle.WaitOne();
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test sequence failed");
            throw;
        }
    }

    private void InitializePython()
    {
        try
        {
            // Only initialize if not already initialized
            if (!_isInitialized || !PythonEngine.IsInitialized)
            {
                Initialize();
            }

            using (Py.GIL())
            {
                try
                {
                    // Import sys module
                    dynamic sys;
                    try
                    {
                        sys = Py.Import("sys");
                        if (sys == null)
                        {
                            throw new InvalidOperationException("Failed to import sys module");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import sys module");
                        throw;
                    }

                    // Log Python version - use a simpler approach
                    try 
                    {
                        string version = PythonEngine.Version;
                        _logger.LogInformation("Python version: {Version}", version);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get Python version");
                    }
                    
                    // Add current directory to Python path
                    string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                    _logger.LogInformation("Adding Python script path: {Path}", currentDir);
                    
                    // Add path directly using dynamic access
                    try
                    {
                        var paths = new List<string>();
                        dynamic sysModule = Py.Import("sys");
                        using var sysPath = sysModule.GetAttr("path");
                        
                        // Convert Python list to C# list safely
                        foreach (PyObject item in sysPath)
                        {
                            string pathStr = item?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(pathStr))
                            {
                                paths.Add(pathStr);
                            }
                        }

                        // Add current directory if not present
                        if (!paths.Contains(currentDir))
                        {
                            sysPath.InvokeMethod("insert", new PyObject[] { new PyInt(0), new PyString(currentDir) });
                            paths.Insert(0, currentDir);
                        }

                        _logger.LogDebug("Python path: {Path}", string.Join(Environment.NewLine, paths));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to modify Python path");
                        throw;
                    }
                    
                    // Clear existing module if it exists
                    try
                    {
                        dynamic modules = sys.modules;
                        if (modules.__contains__("test_pythonnet"))
                        {
                            modules.__delitem__("test_pythonnet");
                        }
                        _testModule = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error clearing existing test module");
                    }
                    
                    // Import the test module
                    try
                    {
                        _logger.LogInformation("Importing test module...");
                        _testModule = Py.Import("test_pythonnet");
                        
                        if (_testModule == null)
                        {
                            throw new InvalidOperationException("Failed to import test module");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import test module");
                        throw;
                    }
                    
                    // Register cleanup callback
                    _logger.LogInformation("Registering cleanup callback...");
                    try
                    {
                        bool callbackResult = _testModule.set_dotnet_callback(new Action(CleanupCallback));
                        if (callbackResult)
                        {
                            _logger.LogInformation("Cleanup callback registered successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Cleanup callback registration returned false");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to register cleanup callback");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Python environment");
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Python module cleanup");
            throw; // Use throw without argument to preserve stack trace
        }
        {
            // If we hit a critical error, try to clean up
            try
            {
                if (_isInitialized && PythonEngine.IsInitialized)
                {
                    // Force shutdown without serialization
                    var runtimeType = typeof(Runtime);
                    var dataField = runtimeType.GetField("data", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (dataField != null)
                    {
                        dataField.SetValue(null, null);
                    }
                    _isInitialized = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Python module cleanup");
                throw; // Re-throw original exception
            }
        }
    }

    private void RunSingleTest(string testName, Func<bool> testFunc)
    {
        _logger.LogInformation("Starting {TestName}...", testName);
        try
        {
            bool result = testFunc();
            if (!result)
            {
                throw new Exception($"{testName} failed");
            }
            _logger.LogInformation("{TestName} completed successfully", testName);
            
            // Wait for cleanup callback with timeout
            WaitForCleanup(TimeSpan.FromSeconds(5));

            // Reset Python state after test
            if (_isInitialized && PythonEngine.IsInitialized)
            {
                try
                {
                    _logger.LogDebug("Shutting down Python engine...");
                    
                    // Force cleanup without serialization
                    using (Py.GIL())
                    {
                        try
                        {
                            dynamic sys = Py.Import("sys");
                            dynamic gc = Py.Import("gc");
                            gc.collect();
                            sys.modules.clear();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error during Python module cleanup");
                        }
                    }
                    
                    // Force shutdown without serialization
                    var runtimeType = typeof(Runtime);
                    var dataField = runtimeType.GetField("data", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (dataField != null)
                    {
                        dataField.SetValue(null, null);
                    }
                    
                    PythonEngine.Shutdown();
                    _isInitialized = false;
                    _testModule = null;
                    _logger.LogInformation("Python engine shut down successfully");
                }
                catch (PythonException pex)
                {
                    // Ignore Python exit exceptions as they are expected
                    if (!pex.Message.Contains("exit") && !pex.Message.Equals("0"))
                    {
                        _logger.LogError(pex, "Error during Python engine shutdown");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is NotSupportedException && ex.Message.Contains("BinaryFormatter"))
                    {
                        // Ignore BinaryFormatter serialization errors
                        _logger.LogDebug("Ignored BinaryFormatter serialization during shutdown");
                    }
                    else
                    {
                        _logger.LogError(ex, "Error during Python engine shutdown");
                        throw;
                    }
                }
            }

            // Reset cleanup event for next test
            _cleanupEvent.Reset();
        }
        catch (PythonException pex)
        {
            // Ignore Python exit exceptions as they are expected
            if (!pex.Message.Contains("exit") && !pex.Message.Equals("0"))
            {
                _logger.LogError(pex, "{TestName} failed", testName);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{TestName} failed", testName);
            throw;
        }
    }

    public void WaitForCleanup(TimeSpan timeout)
    {
        _logger.LogInformation("Waiting for cleanup signal from Python...");
        if (_cleanupEvent.Wait(timeout))
        {
            _logger.LogInformation("Cleanup signal received");
        }
        else
        {
            _logger.LogWarning("Cleanup timeout expired");
        }
    }

    public void StopTest()
    {
        // If we're already cleaned up, just return
        if (_testModule == null || !PythonEngine.IsInitialized)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping test and cleaning up resources...");
            
            using (Py.GIL())
            {
                // Clear the module reference
                _testModule = null;
            }
            
            _logger.LogInformation("Test stopped and resources cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping test");
        }
    }

    public void Dispose()
    {
        if (_isDisposing) return;
        _isDisposing = true;

        try
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cleanupEvent.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }
    }
}