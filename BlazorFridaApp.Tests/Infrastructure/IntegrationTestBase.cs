using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;
using Grpc.Net.Client;
using OpenTelemetry.Context.Propagation;
using Grpc.Core;
using System.Diagnostics;

namespace BlazorFridaApp.Tests.Infrastructure;

public class IntegrationTestBase : BaseGrpcService, IAsyncLifetime
{
    protected readonly IPythonProcessManager ProcessManager;
    protected readonly ILogger<IntegrationTestBase> Logger;
    private bool _disposed;
    protected readonly TestGrpcConfiguration Configuration;

    public IntegrationTestBase() : base(LoggerFactory.Create(builder => 
        builder.AddConsole().SetMinimumLevel(LogLevel.Debug)))
    {
        Configuration = new TestGrpcConfiguration();
        Logger = LoggerFactory.CreateLogger<IntegrationTestBase>();
        Logger.LogInformation("[IntegrationTestBase] Constructor started");
        
        var processManagerLogger = LoggerFactory.CreateLogger<PythonProcessManager>();
        ProcessManager = new PythonProcessManager(processManagerLogger, Configuration.Server.Port);
        
        ChannelOptions = Configuration.CreateChannelOptions();
        Logger.LogInformation("[IntegrationTestBase] Created PythonProcessManager instance");
    }

    protected override string GetEndpoint() => Configuration.Server.Endpoint;

    public virtual async Task InitializeAsync()
    {
        Logger.LogInformation("[IntegrationTestBase] InitializeAsync started");
        
        for (int attempt = 1; attempt <= Configuration.Retry.MaxAttempts; attempt++)
        {
            try
            {
                Logger.LogInformation("[InitializeAsync] Connection attempt {Attempt} of {MaxAttempts} to port {Port}", 
                    attempt, Configuration.Retry.MaxAttempts, ProcessManager.Port);
                
                await ProcessManager.VerifyConnection();
                Logger.LogInformation("[InitializeAsync] Successfully connected to gRPC server on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[InitializeAsync] Connection attempt {Attempt} failed. Error details: {ErrorMessage}", 
                    attempt, ex.ToString());
                
                if (attempt == Configuration.Retry.MaxAttempts)
                {
                    Logger.LogError("[InitializeAsync] All connection attempts failed after {MaxAttempts} tries", Configuration.Retry.MaxAttempts);
                    throw new InvalidOperationException(
                        $"Failed to connect to gRPC server after {Configuration.Retry.MaxAttempts} attempts. Please ensure the server is running by executing 'start_test_server.bat' before running tests. Last error: {ex.Message}", 
                        ex);
                }
                
                Logger.LogInformation("[InitializeAsync] Waiting {Delay}ms before next attempt", Configuration.Retry.DelayMs);
                await Task.Delay(Configuration.Retry.DelayMs);
            }
        }
    }

    public virtual async Task DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                Logger.LogInformation("[IntegrationTestBase] DisposeAsync started");
                await base.DisposeChannelsAsync();

                if (ProcessManager is IDisposable disposable)
                {
                    disposable.Dispose();
                    Logger.LogInformation("[IntegrationTestBase] ProcessManager disposed");
                }
                
                Logger.LogInformation("[IntegrationTestBase] DisposeAsync completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[IntegrationTestBase] Error during test cleanup");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}