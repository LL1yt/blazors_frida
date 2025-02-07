using Microsoft.Extensions.Configuration;

namespace BlazorFridaApp.Services;

public interface IFeatureFlagService
{
    bool IsGrpcServiceEnabled();
    bool IsOperationEnabled(string operationType);
    int GetOperationTrafficPercentage(string operationType);
    bool ShouldUseNewImplementation(string operationType);
    bool IsMonitoringEnabled(string monitoringType);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfiguration _configuration;
    private readonly Random _random;

    public FeatureFlagService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _random = new Random();
    }

    public bool IsGrpcServiceEnabled()
    {
        return _configuration.GetValue<bool>("FeatureFlags:UseGrpcService");
    }

    public bool IsOperationEnabled(string operationType)
    {
        if (string.IsNullOrEmpty(operationType))
            throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));

        return _configuration.GetValue<bool>($"FeatureFlags:Operations:{operationType}:UseNewImplementation");
    }

    public int GetOperationTrafficPercentage(string operationType)
    {
        if (string.IsNullOrEmpty(operationType))
            throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));

        return _configuration.GetValue<int>($"FeatureFlags:Operations:{operationType}:TrafficPercentage");
    }

    public bool ShouldUseNewImplementation(string operationType)
    {
        if (string.IsNullOrEmpty(operationType))
            throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));

        if (!IsGrpcServiceEnabled()) return false;
        if (!IsOperationEnabled(operationType)) return false;

        var trafficPercentage = GetOperationTrafficPercentage(operationType);
        if (trafficPercentage >= 100) return true;
        if (trafficPercentage <= 0) return false;

        // Randomly route traffic based on percentage
        return _random.Next(100) < trafficPercentage;
    }

    public bool IsMonitoringEnabled(string monitoringType)
    {
        if (string.IsNullOrEmpty(monitoringType))
            throw new ArgumentException("Monitoring type cannot be null or empty", nameof(monitoringType));

        return _configuration.GetValue<bool>($"FeatureFlags:Monitoring:{monitoringType}Enabled");
    }
}