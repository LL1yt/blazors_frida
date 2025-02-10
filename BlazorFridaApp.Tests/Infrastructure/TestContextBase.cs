using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BlazorFridaApp.Services;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorFridaApp.Tests;

public class TestContextBase : TestContext
{
    protected TestContextBase()
    {
        Services.AddBlazorise(options =>
        {
            options.Immediate = true;
        })
        .AddBootstrapProviders()
        .AddFontAwesomeIcons();

        // Mock INotificationService for tests
        var notificationServiceMock = new Mock<INotificationService>();
        Services.AddScoped<INotificationService>(_ => notificationServiceMock.Object);
        
        // Register logger
        var loggerMock = new Mock<ILogger>();
        Services.AddScoped(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<ILoggerProvider>(_ => new MockLoggerProvider());
        
        // Register other required services
        Services.AddScoped<DialogService>();
    }
}

// Mock logger provider for tests
public class MockLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Mock<ILogger>().Object;
    public void Dispose() { }
}