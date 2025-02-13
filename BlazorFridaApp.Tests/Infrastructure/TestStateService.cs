using BlazorFridaApp.MemoryScanner.Services;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.Tests.Infrastructure;

public interface ITestStateService
{
    Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(string sessionId, string checkpointId);
    Task<(bool success, string error, string newVersion)> SyncStateAsync(
        string sessionId,
        Dictionary<string, byte[]> stateUpdates,
        string version);
}

public class TestStateService : BaseTestGrpcService, ITestStateService
{
    private readonly IStateGrpcService _stateService;

    public TestStateService(
        ILogger<StateGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _stateService = new StateGrpcService(logger, processManager);
    }

    public Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(string sessionId, string checkpointId)
    {
        return _stateService.GetStateAsync(sessionId, checkpointId);
    }

    public Task<(bool success, string error, string newVersion)> SyncStateAsync(
        string sessionId,
        Dictionary<string, byte[]> stateUpdates,
        string version)
    {
        return _stateService.SyncStateAsync(sessionId, stateUpdates, version);
    }
}