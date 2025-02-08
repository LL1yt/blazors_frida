using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services;
using Xunit;

namespace BlazorFridaApp.Tests.Infrastructure;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly ILogger<IntegrationTestBase> Logger;
    protected readonly PythonProcessManager ProcessManager;

    public IntegrationTestBase()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
        Logger = factory.CreateLogger<IntegrationTestBase>();
        ProcessManager = new PythonProcessManager(factory.CreateLogger<PythonProcessManager>());
    }

    public async Task InitializeAsync()
    {
        await ProcessManager.EnsureServerRunning();
    }

    public async Task DisposeAsync()
    {
        await ProcessManager.StopServer();
        ProcessManager.Dispose();
    }
} 