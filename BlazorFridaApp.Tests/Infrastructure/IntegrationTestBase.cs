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
        Logger.LogInformation("[IntegrationTestBase] Constructor started");
        
        var processManagerLogger = factory.CreateLogger<PythonProcessManager>();
        ProcessManager = new PythonProcessManager(processManagerLogger);
        Logger.LogInformation("[IntegrationTestBase] Created PythonProcessManager instance");
    }

    public async Task InitializeAsync()
    {
        Logger.LogInformation("[IntegrationTestBase] InitializeAsync started");
        await ProcessManager.EnsureServerRunning();
        Logger.LogInformation("[IntegrationTestBase] Server startup completed");
        await Task.Delay(2000); // Give server some time to fully initialize
        Logger.LogInformation("[IntegrationTestBase] InitializeAsync completed after delay");
    }

    public async Task DisposeAsync()
    {
        try
        {
            Logger.LogInformation("[IntegrationTestBase] DisposeAsync started");
            if (ProcessManager is IDisposable disposable)
            {
                disposable.Dispose();
                Logger.LogInformation("[IntegrationTestBase] ProcessManager disposed");
            }
            await Task.CompletedTask;
            Logger.LogInformation("[IntegrationTestBase] DisposeAsync completed");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[IntegrationTestBase] Error during test cleanup");
        }
    }
} 