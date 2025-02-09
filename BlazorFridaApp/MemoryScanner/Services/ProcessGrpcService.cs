using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services;

public class ProcessGrpcService : BaseGrpcService, IProcessGrpcService
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get accessible processes");
            throw;
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