using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Integration;

public class MemoryScannerIntegrationTests : IntegrationTestBase
{
    private readonly ScannerGrpcService _scannerService;
    private readonly ILogger<ScannerGrpcService> _scannerLogger;

    public MemoryScannerIntegrationTests()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
        _scannerLogger = factory.CreateLogger<ScannerGrpcService>();
        _scannerService = new ScannerGrpcService(_scannerLogger, ProcessManager);
    }

    [Theory]
    [InlineData(MemoryValueType.Byte, 1)]
    [InlineData(MemoryValueType.Short, 2)]
    [InlineData(MemoryValueType.Int, 4)]
    [InlineData(MemoryValueType.Long, 8)]
    [InlineData(MemoryValueType.Float, 4)]
    [InlineData(MemoryValueType.Double, 8)]
    public async Task ShouldScanWithDifferentValueTypes(MemoryValueType valueType, int expectedSize)
    {
        // Arrange
        var processId = 1234; // Use a test process ID
        var value = 42;

        // Act
        var result = await _scannerService.ScanForValue(processId, value, valueType);

        // Assert
        Assert.NotNull(result);
        // Note: We can't assert much about the actual results since they depend on the process memory
        // But at least we know the call succeeded
    }

    [Fact]
    public async Task ShouldScanWithPattern()
    {
        // Arrange
        var processId = 1234;
        var pattern = new byte[] { 0xAA, 0xBB, 0xCC };
        var mask = "xxx";

        // Act
        var result = await _scannerService.ScanForPattern(processId, pattern, mask);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ShouldScanWithProfile()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var profile = new ScanProfile 
        { 
            ComparisonType = "exact",
            ValueType = MemoryValueType.Int
        };

        // Act
        var result = await _scannerService.ScanAsync(
            processInfo,
            "42",
            (int)ScanType.ExactValue,
            profile);

        // Assert
        Assert.NotNull(result);
    }
} 