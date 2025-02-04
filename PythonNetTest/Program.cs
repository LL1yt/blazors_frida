using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PythonNetTest;
using System.Runtime.InteropServices;
using System.Threading;

namespace PythonNetTest;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? HandlerRoutine, bool Add);

    public delegate bool ConsoleCtrlDelegate(uint CtrlType);
}

public class Program
{
    private static TestPythonNet? _test;
    private static ILogger? _logger;
    private static bool _forceStopCalled;

    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole();
        });
        _logger = loggerFactory.CreateLogger("PythonNetTest");
        _forceStopCalled = false;

        try
        {
            _logger.LogInformation("Starting Python.NET test program");
            _test = new TestPythonNet(_logger);
            
            // Run the test
            _test.RunTest();
            
            _logger.LogInformation("Tests completed. Press Enter to force stop Python processes, or Ctrl+C to test application responsiveness...");
            
            // Wait for Enter key
            while (true)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    _logger.LogInformation("Enter key pressed, initiating force stop...");
                    if (_test != null)
                    {
                        _test.ForceStop();
                        _forceStopCalled = true;
                    }
                    _logger.LogInformation("Python processes stopped. Press Ctrl+C to exit the application...");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test failed");
        }
        finally
        {
            if (_test != null && !_forceStopCalled)
            {
                try
                {
                    _test.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cleanup");
                }
            }
            _logger.LogInformation("Program finished");
        }
    }
}