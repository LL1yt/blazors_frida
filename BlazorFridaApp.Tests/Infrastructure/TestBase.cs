using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Services;
using Microsoft.Extensions.Logging;
using Moq;
using BlazorFridaApp.MemoryScanner.Proto.Health;

namespace BlazorFridaApp.Tests.Infrastructure;

public abstract class TestBase : TestContext
{
    protected readonly TestContext Context;
    protected readonly Mock<IProcessMemoryScanner> ScannerMock;
    protected readonly Mock<Health.HealthClient> HealthClientMock;
    protected readonly Mock<ILogger<PythonProcessManager>> LoggerMock;

    protected TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .Build();
        var serviceFactory = new TestServiceFactory(configuration);
            
        Context = new TestContext(serviceFactory, "test");
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        ScannerMock = new Mock<IProcessMemoryScanner>();
        LoggerMock = new Mock<ILogger<PythonProcessManager>>();
        HealthClientMock = GrpcTestHelper.CreateHealthClientMock(LoggerMock.Object);
        
        // Add any common services here
        Context.Services.AddScoped<IServiceProvider>(sp => sp);
        
        // Configure services
        ConfigureServices(Context.Services);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Register notification service mock
        var notificationServiceMock = new Mock<INotificationService>();
        services.AddScoped<INotificationService>(_ => notificationServiceMock.Object);
        
        // Register scanner mock
        services.AddScoped<IProcessMemoryScanner>(_ => ScannerMock.Object);

        // Register PythonProcessManager with mocked dependencies
        services.AddScoped<IPythonProcessManager>(sp => 
            new PythonProcessManager(LoggerMock.Object, 50051, HealthClientMock.Object));
    }
}