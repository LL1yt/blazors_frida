using BlazorFridaApp.MemoryScanner.Services;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.Tests.Infrastructure;

public interface ITestMemoryService
{
    Task<(byte[] value, bool success, string error)> ReadMemoryAsync(string sessionId, ulong address, int size);
    Task<(bool success, string error)> WriteMemoryAsync(string sessionId, ulong address, byte[] data);
}

public class TestMemoryService : BaseTestGrpcService, ITestMemoryService
{
    private readonly IMemoryGrpcService _memoryService;

    public TestMemoryService(
        ILogger<TestMemoryService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _memoryService = new MemoryGrpcService(logger, processManager);
    }

    public async Task<(byte[] value, bool success, string error)> ReadMemoryAsync(string sessionId, ulong address, int size)
    {
        return await _memoryService.ReadMemoryBytes(sessionId, address, size);
    }

    public async Task<(bool success, string error)> WriteMemoryAsync(string sessionId, ulong address, byte[] data)
    {
        return await _memoryService.WriteMemoryBytes(sessionId, address, data);
    }
}