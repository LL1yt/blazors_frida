using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Models;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace BlazorFridaApp.Tests.Infrastructure;

public static class TestHelper
{
    public static async Task<TestContext> CreateTestContextAsync(bool useMocks = false)
    {
        var config = new TestGrpcConfiguration();
        var factory = useMocks 
            ? new MockTestServiceFactory(config) 
            : new TestServiceFactory(config);

        // Verify service health before creating context
        var healthService = factory.CreateHealthService();
        var health = await healthService.CheckHealthAsync();
        
        if (health.Status != MemoryScanner.Proto.Health.HealthCheckResponse.Types.ServingStatus.Serving)
        {
            throw new InvalidOperationException($"gRPC service is not healthy. Status: {health.Status}");
        }

        return new TestContext(factory);
    }

    public static async Task WithTestContextAsync(Func<TestContext, Task> testAction, bool useMocks = false)
    {
        await using var context = await CreateTestContextAsync(useMocks);
        await testAction(context);
    }

    public static async Task<TResult> WithTestContextAsync<TResult>(
        Func<TestContext, Task<TResult>> testAction, 
        bool useMocks = false)
    {
        await using var context = await CreateTestContextAsync(useMocks);
        return await testAction(context);
    }

    public static async Task<ProcessInfo> FindTestProcessAsync(
        TestContext context,
        string processName,
        ILogger logger)
    {
        logger.LogInformation("Looking for process: {ProcessName}", processName);
        var processes = await context.ProcessService.GetProcessesAsync();
        var process = processes.FirstOrDefault(p => p.Name.Equals(processName, StringComparison.OrdinalIgnoreCase));

        if (process == null)
        {
            throw new InvalidOperationException($"Process {processName} not found. Please ensure it is running.");
        }

        logger.LogInformation("Found process {ProcessName} with ID: {ProcessId}", processName, process.Id);
        return process;
    }

    public static async Task<string> AttachToProcessAsync(
        TestContext context,
        int processId,
        ILogger logger)
    {
        logger.LogInformation("Attaching to process ID: {ProcessId}", processId);
        var (success, sessionId) = await context.ProcessService.AttachToProcessAsync(processId);

        if (!success || string.IsNullOrEmpty(sessionId))
        {
            throw new InvalidOperationException($"Failed to attach to process {processId}");
        }

        logger.LogInformation("Successfully attached to process. Session ID: {SessionId}", sessionId);
        return sessionId;
    }
}