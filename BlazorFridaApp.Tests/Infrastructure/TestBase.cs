using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Base;
using Moq;

namespace BlazorFridaApp.Tests.Infrastructure;

public abstract class TestBase : TestContext
{
    protected readonly TestContext Context;
    protected readonly Mock<IProcessMemoryScanner> ScannerMock;

    protected TestBase()
    {
        Context = new TestContext();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        ScannerMock = new Mock<IProcessMemoryScanner>();
        
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
    }
}