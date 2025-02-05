using Microsoft.Extensions.Diagnostics.HealthChecks;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Grpc.Net.Client;
using BlazorFridaApp.MemoryScanner.Proto.Health;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.Services;

public class GrpcHealthCheck : IHealthCheck
{
    private readonly IPythonProcessManager _pythonProcessManager;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<GrpcHealthCheck> _logger;

    public GrpcHealthCheck(
        IPythonProcessManager pythonProcessManager,
        IFeatureFlagService featureFlagService,
        ILogger<GrpcHealthCheck> logger)
    {
        _pythonProcessManager = pythonProcessManager;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_featureFlagService.IsGrpcServiceEnabled())
            {
                return HealthCheckResult.Healthy("gRPC service is not enabled");
            }

            var port = _pythonProcessManager.Port;
            _logger.LogInformation("Ensuring Python process is running...");
            await _pythonProcessManager.EnsureServerRunning();

            _logger.LogInformation("Attempting to connect to gRPC server on port {Port}", port);

            // Perform actual gRPC health check
            var channelOptions = new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
                }
            };

            var address = $"http://localhost:{port}";
            _logger.LogInformation("Creating gRPC channel to {Address}", address);

            using var channel = GrpcChannel.ForAddress(address, channelOptions);
            _logger.LogDebug("Created gRPC channel, creating health client...");
            var client = new Health.HealthClient(channel);
            
            try
            {
                _logger.LogDebug("Sending health check request...");
                var request = new HealthCheckRequest { Service = "" };
                var response = await client.CheckAsync(request, 
                    deadline: DateTime.UtcNow.AddSeconds(5), 
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Received health check response with status: {Status}", response.Status);

                if (response.Status == HealthCheckResponse.Types.ServingStatus.Serving)
                {
                    return HealthCheckResult.Healthy("gRPC service is healthy");
                }
                else
                {
                    _logger.LogWarning("gRPC service reported non-serving status: {Status}", response.Status);
                    return HealthCheckResult.Unhealthy($"gRPC service reported non-serving status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to gRPC service at {Address}", address);
                return HealthCheckResult.Unhealthy($"Failed to connect to gRPC service at {address}", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}