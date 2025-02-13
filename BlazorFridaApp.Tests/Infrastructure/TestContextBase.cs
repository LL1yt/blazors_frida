using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BlazorFridaApp.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorFridaApp.Tests;

public class TestContextBase : TestContext, IAsyncDisposable
{
    protected TestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Mock INotificationService for tests
        var notificationServiceMock = new Mock<BlazorFridaApp.Services.INotificationService>();
        Services.AddScoped<BlazorFridaApp.Services.INotificationService>(_ => notificationServiceMock.Object);
        
        // Register logger
        var loggerMock = new Mock<ILogger>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        Services.AddSingleton<ILoggerFactory>(loggerFactory);
        Services.AddScoped(typeof(ILogger<>), typeof(Logger<>));
        
        // Register other required services
        Services.AddScoped<DialogService>();
    }

    public new void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public virtual async ValueTask DisposeAsync()
    {
        Dispose();
        await ValueTask.CompletedTask;
    }
}

// Mock logger provider for tests
public class MockLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Mock<ILogger>().Object;
    public void Dispose() { }
}