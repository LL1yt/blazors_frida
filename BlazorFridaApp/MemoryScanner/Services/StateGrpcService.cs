using BlazorFridaApp.MemoryScanner.Services.Base;
using Microsoft.Extensions.Logging;
using Google.Protobuf;

namespace BlazorFridaApp.MemoryScanner.Services;

public class StateGrpcService : BaseGrpcService
{
    private string _currentSessionId = string.Empty;

    public StateGrpcService(
        ILogger<StateGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
    }

    public async Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(
        string sessionId,
        string checkpointId)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var request = new Proto.StateRequest
            {
                SessionId = sessionId,
                CheckpointId = checkpointId ?? string.Empty
            };

            var response = await client.GetStateAsync(request, CreateMetadata());
            
            if (response == null)
            {
                return (new Dictionary<string, byte[]>(), string.Empty);
            }

            var state = response.State.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToByteArray()
            );

            return (state, response.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get state for session {SessionId} and checkpoint {CheckpointId}", sessionId, checkpointId);
            return (new Dictionary<string, byte[]>(), string.Empty);
        }
    }

    public async Task<(bool success, string error, string newVersion)> SyncStateAsync(
        string sessionId,
        Dictionary<string, byte[]> stateUpdates,
        string version)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var request = new Proto.SyncRequest
            {
                SessionId = sessionId,
                Version = version ?? string.Empty
            };

            foreach (var kvp in stateUpdates)
            {
                request.StateUpdates[kvp.Key] = ByteString.CopyFrom(kvp.Value);
            }

            var response = await client.SyncStateAsync(request, CreateMetadata());

            if (response == null)
            {
                return (false, "No response received from server", string.Empty);
            }

            return (response.Success, response.ErrorMessage, response.NewVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync state for session {SessionId}", sessionId);
            return (false, ex.Message, string.Empty);
        }
    }
}