using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BlazorFridaApp.Tests.Infrastructure;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IPythonProcessManager ProcessManager;
    protected readonly ILogger<IntegrationTestBase> Logger;

    public IntegrationTestBase()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
        Logger = factory.CreateLogger<IntegrationTestBase>();
        var processManagerLogger = factory.CreateLogger<PythonProcessManager>();
        ProcessManager = new PythonProcessManager(processManagerLogger);
    }

    public async Task InitializeAsync()
    {
        await ProcessManager.EnsureServerRunning();
        await Task.Delay(2000); // Give server some time to fully initialize
    }

    public async Task DisposeAsync()
    {
        try
        {
            // Cleanup if needed
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during test cleanup");
        }
    }
} 