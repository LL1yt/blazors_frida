using BlazorFridaApp.MemoryScanner.Proto.Health;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services;
using Grpc.Core;

namespace BlazorFridaApp.Tests.Infrastructure;

public class TestHealthService : BaseTestGrpcService
{
    public TestHealthService(
        ILogger<TestHealthService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
    }

    public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync();
        var client = new Health.HealthClient(channel);
        var request = new HealthCheckRequest { Service = "" };
        
        return await client.CheckAsync(request, 
            deadline: DateTime.UtcNow.AddSeconds(5),
            cancellationToken: cancellationToken);
    }
} 