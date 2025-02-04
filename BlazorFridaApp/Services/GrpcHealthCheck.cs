using Microsoft.Extensions.Diagnostics.HealthChecks;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

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

            var isRunning = _pythonProcessManager.IsProcessRunning();
            if (!isRunning)
            {
                return HealthCheckResult.Unhealthy("Python gRPC server is not running");
            }

            // Add additional checks here as needed
            // For example, try to make a simple gRPC call

            return HealthCheckResult.Healthy("gRPC service is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}