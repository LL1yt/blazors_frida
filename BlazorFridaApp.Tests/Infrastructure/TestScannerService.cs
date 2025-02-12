using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.Tests.Infrastructure;

public interface ITestScannerService
{
    Task<IEnumerable<ScanResult>> ScanMemoryAsync(
        string sessionId,
        string valueType,
        byte[] value,
        string comparisonType,
        IEnumerable<(ulong start, ulong end)> ranges);
}

public class TestScannerService : BaseTestGrpcService, ITestScannerService
{
    private readonly IScannerGrpcService _scannerService;

    public TestScannerService(
        ILogger<TestScannerService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
        _scannerService = new ScannerGrpcService(logger, processManager);
    }

    public Task<IEnumerable<ScanResult>> ScanMemoryAsync(
        string sessionId,
        string valueType,
        byte[] value,
        string comparisonType,
        IEnumerable<(ulong start, ulong end)> ranges)
    {
        return _scannerService.ScanAsync(sessionId, valueType, value, comparisonType, ranges);
    }
}