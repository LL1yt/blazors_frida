using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Grpc.Core;

namespace BlazorFridaApp.MemoryScanner.Services;

public class ProcessGrpcService : BaseGrpcService, IProcessGrpcService, IProcessService
{
    private string _currentSessionId = string.Empty;
    private nint _processHandle;
    public nint ProcessHandle => _processHandle;

    public ProcessGrpcService(
        ILogger<ProcessGrpcService> logger,
        IPythonProcessManager processManager) 
        : base(logger, processManager)
    {
    }

    public async Task<List<ProcessInfo>> GetAccessibleProcessesAsync()
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            var request = new Proto.Empty();
            var response = await client.ListProcessesAsync(request, CreateMetadata());
            
            return response.Processes.Select(p => new ProcessInfo
            {
                Id = p.Pid,
                Name = p.Name,
                Path = p.Path
            }).ToList();
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Internal)
        {
            var errorMessage = ex.Status.Detail;
            if (errorMessage.Contains("Invalid argument") || errorMessage.Contains("TimedOutError"))
            {
                _logger.LogWarning("Failed to connect to Frida device. Error: {Error}", errorMessage);
                // Force channel recreation by removing it from the dictionary
                var endpoint = $"http://127.0.0.1:{_processManager.Port}";
                if (_channels.TryRemove(endpoint, out var oldChannel))
                {
                    await oldChannel.ShutdownAsync().ConfigureAwait(false);
                }
                
                try 
                {
                    // Retry with a new channel
                    var channel = await GetChannelAsync();
                    var client = CreateClient(channel);
                    var request = new Proto.Empty();
                    var response = await client.ListProcessesAsync(request, CreateMetadata());
                    
                    return response.Processes.Select(p => new ProcessInfo
                    {
                        Id = p.Pid,
                        Name = p.Name,
                        Path = p.Path
                    }).ToList();
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Failed to enumerate processes even after retry");
                    throw new InvalidOperationException("Failed to access process list. Please ensure Frida server is running with sufficient privileges.", retryEx);
                }
            }
            _logger.LogError(ex, "Internal error while getting accessible processes");
            throw new InvalidOperationException("An internal error occurred while accessing the process list.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting accessible processes");
            throw new InvalidOperationException("An unexpected error occurred while accessing the process list.", ex);
        }
    }

    public async Task<ProcessInfo> GetTargetProcessAsync()
    {
        var processes = await GetAccessibleProcessesAsync();
        return processes.FirstOrDefault() ?? throw new InvalidOperationException("No accessible processes found");
    }

    public async Task<(bool success, string sessionId)> AttachToProcessAsync(int pid)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            
            var request = new Proto.ProcessRequest { Pid = pid };
            var response = await client.AttachToProcessAsync(request, CreateMetadata());
            
            if (response.Success)
            {
                _currentSessionId = response.SessionId;
                _processHandle = new nint(pid);
            }
            
            return (response.Success, response.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to process {Pid}", pid);
            return (false, string.Empty);
        }
    }

    public async Task DetachFromProcessAsync(string sessionId)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);
            
            var request = new Proto.DetachRequest { SessionId = sessionId };
            await client.DetachFromProcessAsync(request, CreateMetadata());
            
            if (sessionId == _currentSessionId)
            {
                _currentSessionId = string.Empty;
                _processHandle = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detach from process {SessionId}", sessionId);
            throw;
        }
    }
}