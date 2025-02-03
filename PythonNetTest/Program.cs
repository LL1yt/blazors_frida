using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PythonNetTest;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Trace)
        .AddConsole();
});
var logger = loggerFactory.CreateLogger("PythonNetTest");

try
{
    logger.LogInformation("Starting Python.NET test program");
    var test = new TestPythonNet(logger);
    test.RunTest();
    logger.LogInformation("Test completed successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Test failed");
}