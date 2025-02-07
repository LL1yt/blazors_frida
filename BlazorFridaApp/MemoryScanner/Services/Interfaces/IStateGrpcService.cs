using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces;

public interface IStateGrpcService
{
    Task<(Dictionary<string, byte[]> state, string version)> GetStateAsync(string sessionId, string checkpointId);
    Task<(bool success, string error, string newVersion)> SyncStateAsync(string sessionId, Dictionary<string, byte[]> stateUpdates, string version);
}