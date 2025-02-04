using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PythonNetTest;
using System.Runtime.InteropServices;

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
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole();
        });
        var logger = loggerFactory.CreateLogger("PythonNetTest");

        TestPythonNet? test = null;
        try
        {
            logger.LogInformation("Starting Python.NET test program");
            test = new TestPythonNet(logger);
            
            // Run the test
            test.RunTest();
            
            logger.LogInformation("Tests completed. Press Ctrl+C to exit...");
            
            // Wait for user input
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test failed");
        }
        finally
        {
            if (test != null)
            {
                try
                {
                    test.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during cleanup");
                }
            }
            logger.LogInformation("Program finished");
        }
    }
}