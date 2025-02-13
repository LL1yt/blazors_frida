using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.Tests.Infrastructure;

public class TestServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPythonProcessManager _processManager;
    private readonly TestGrpcConfiguration _configuration;

    public TestServiceFactory(TestGrpcConfiguration configuration)
    {
        _configuration = configuration;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        var processManagerLogger = _loggerFactory.CreateLogger<PythonProcessManager>();
        _processManager = new PythonProcessManager(processManagerLogger, _configuration.Server.Port);
    }

    public virtual ITestHealthService CreateHealthService() => 
        new TestHealthService(_loggerFactory.CreateLogger<TestHealthService>(), _processManager);

    public virtual ITestMemoryService CreateMemoryService() => 
        new TestMemoryService(_loggerFactory.CreateLogger<TestMemoryService>(), _processManager);

    public virtual ITestProcessService CreateProcessService() => 
        new TestProcessService(_loggerFactory.CreateLogger<TestProcessService>(), _processManager);

    public virtual ITestScannerService CreateScannerService() => 
        new TestScannerService(_loggerFactory.CreateLogger<TestScannerService>(), _processManager);

    public virtual ITestStateService CreateStateService() => 
        new TestStateService(_loggerFactory.CreateLogger<TestStateService>(), _processManager);

    public virtual ITestFreezeService CreateFreezeService() => 
        new TestFreezeService(_loggerFactory.CreateLogger<TestFreezeService>(), _processManager);

    public IPythonProcessManager GetProcessManager() => _processManager;
}