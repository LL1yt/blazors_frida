using BlazorFridaApp.MemoryScanner.Services;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.Tests.Infrastructure;

public interface ITestFreezeService
{
    IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken = default);
    Task UnfreezeValueAsync(string sessionId, ulong address);
}

public class TestFreezeService : BaseTestGrpcService, ITestFreezeService
{
    private readonly IFreezeGrpcService _freezeService;

    public TestFreezeService(
        ILogger<TestFreezeService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _freezeService = new FreezeGrpcService(logger, processManager);
    }

    public IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken = default)
    {
        return _freezeService.FreezeValueAsync(sessionId, address, value, valueType, cancellationToken);
    }

    public Task UnfreezeValueAsync(string sessionId, ulong address)
    {
        return _freezeService.UnfreezeValueAsync(sessionId, address);
    }
}