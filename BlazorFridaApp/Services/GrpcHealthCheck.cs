using Microsoft.Extensions.Diagnostics.HealthChecks;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Grpc.Net.Client;
using BlazorFridaApp.MemoryScanner.Proto.Health;

namespace BlazorFridaApp.Services;

public class GrpcHealthCheck : IHealthCheck
{
    private readonly IPythonProcessManager _pythonProcessManager;
    private readonly IFeatureFlagService _featureFlagService;

    public GrpcHealthCheck(
        IPythonProcessManager pythonProcessManager,
        IFeatureFlagService featureFlagService)
    {
        _pythonProcessManager = pythonProcessManager;
        _featureFlagService = featureFlagService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_featureFlagService.IsGrpcServiceEnabled())
            {
                return HealthCheckResult.Healthy("gRPC service is not enabled");
            }

            var isRunning = _pythonProcessManager.IsRunning;
            if (!isRunning)
            {
                return HealthCheckResult.Unhealthy("Python gRPC server is not running");
            }

            // Perform actual gRPC health check
            using var channel = GrpcChannel.ForAddress($"http://localhost:{_pythonProcessManager.Port}");
            var client = new Health.HealthClient(channel);
            
            try
            {
                var request = new HealthCheckRequest { Service = "memory_scanner.MemoryScanner" };
                var response = await client.CheckAsync(request, cancellationToken: cancellationToken);

                if (response.Status == HealthCheckResponse.Types.ServingStatus.Serving)
                {
                    return HealthCheckResult.Healthy("gRPC service is healthy");
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"gRPC service reported non-serving status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to connect to gRPC service", ex);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}