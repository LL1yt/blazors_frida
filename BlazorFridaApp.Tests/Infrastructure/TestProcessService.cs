using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.Tests.Infrastructure;

public interface ITestProcessService
{
    Task<IEnumerable<ProcessInfo>> GetProcessesAsync();
    Task<(bool success, string sessionId)> AttachToProcessAsync(int pid);
    Task DetachFromProcessAsync(string sessionId);
}

public class TestProcessService : BaseTestGrpcService, ITestProcessService
{
    private readonly IProcessGrpcService _processService;

    public TestProcessService(
        ILogger<TestProcessService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _processService = new ProcessGrpcService(logger, processManager);
    }

    public Task<IEnumerable<ProcessInfo>> GetProcessesAsync()
    {
        return _processService.GetAccessibleProcessesAsync();
    }

    public Task<(bool success, string sessionId)> AttachToProcessAsync(int pid)
    {
        return _processService.AttachToProcessAsync(pid);
    }

    public Task DetachFromProcessAsync(string sessionId)
    {
        return _processService.DetachFromProcessAsync(sessionId);
    }
}