using System;
using System.Diagnostics;
using Python.Runtime;
using Microsoft.Extensions.Logging;
using System.IO;

namespace PythonNetTest;

public class TestPythonNet : IDisposable
{
    private readonly ILogger _logger;
    private bool _isInitialized;

    public TestPythonNet(ILogger logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            _logger.LogInformation("Initializing Python engine...");
            
            var pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME");
            if (string.IsNullOrEmpty(pythonHome))
            {
                pythonHome = @"C:\Users\n0n4a\AppData\Local\Programs\Python\Python313";
            }
            
            Runtime.PythonDLL = Path.Combine(pythonHome, "python313.dll");
            _logger.LogInformation("Using Python DLL: {DllPath}", Runtime.PythonDLL);
            
            PythonEngine.Initialize();
            _isInitialized = true;
            _logger.LogInformation("Python initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Python");
            throw;
        }
    }

    public void RunTest()
    {
        try
        {
            Initialize();

            _logger.LogInformation("Starting Python.NET test...");
            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                _logger.LogInformation("Python version: {Version}", (string)sys.version);
                
                _logger.LogInformation("Importing test module...");
                dynamic testModule = Py.Import("test_pythonnet");
                
                _logger.LogInformation("Testing long running function...");
                dynamic result = testModule.long_running_function();
                _logger.LogInformation("Function result: {Result}", (string)result);
                
                _logger.LogInformation("Testing GIL...");
                bool gilTestResult = testModule.test_gil();
                _logger.LogInformation("GIL test result: {Result}", gilTestResult);
                
                _logger.LogInformation("Testing Frida...");
                bool fridaTestResult = testModule.test_frida();
                _logger.LogInformation("Frida test result: {Result}", fridaTestResult);
                
                if (!gilTestResult || !fridaTestResult)
                {
                    throw new Exception("One or more tests failed");
                }
            }
            _logger.LogInformation("Test completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test failed");
            throw;
        }
    }

    public void Dispose()
    {
        if (_isInitialized && PythonEngine.IsInitialized)
        {
            try
            {
                PythonEngine.Shutdown();
                _isInitialized = false;
                _logger.LogInformation("Python engine shut down successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down Python engine");
            }
        }
    }
}