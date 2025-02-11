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

    private ProcessInfo GetTestProcess()
    {
        var notepadProcess = System.Diagnostics.Process.GetProcessesByName("notepad").FirstOrDefault();
        
        if (notepadProcess == null)
        {
            _scannerLogger.LogWarning("Notepad.exe process not found. Please start Notepad.exe before running tests.");
            throw new InvalidOperationException("Notepad.exe process not found. Please start Notepad.exe before running tests.");
        }

        return new ProcessInfo 
        { 
            Id = notepadProcess.Id,
            Name = notepadProcess.ProcessName,
            Path = notepadProcess.MainModule?.FileName ?? "notepad.exe"
        };
    }

    [Theory]
    [InlineData(MemoryValueType.Byte, "uint8", 1)]
    [InlineData(MemoryValueType.Int16, "int16", 2)]
    [InlineData(MemoryValueType.Int32, "int32", 4)]
    [InlineData(MemoryValueType.Int64, "int64", 8)]
    [InlineData(MemoryValueType.Float, "float", 4)]
    [InlineData(MemoryValueType.Double, "double", 8)]
    public async Task ShouldScanWithDifferentValueTypes(MemoryValueType valueType, string expectedFridaType, int expectedSize)
    {
        // Arrange
        var processInfo = GetTestProcess();
        // Attach to process first
        await _scannerService.AttachToProcessAsync(processInfo);
        var value = 42;

        // Act
        var result = await _scannerService.ScanForValue(processInfo.Id, value, valueType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSize, GetValueTypeSize(valueType));
        
        // Verify that the correct Frida type was used (this requires exposing the mapped type through the service)
        var mappedType = ((ScannerGrpcService)_scannerService).MapValueType(valueType.ToString());
        Assert.Equal(expectedFridaType, mappedType);
    }

    private int GetValueTypeSize(MemoryValueType valueType) => valueType switch
    {
        MemoryValueType.Byte => 1,
        MemoryValueType.Int16 => 2,
        MemoryValueType.Int32 => 4,
        MemoryValueType.Int64 => 8,
        MemoryValueType.Float => 4,
        MemoryValueType.Double => 8,
        _ => throw new ArgumentException($"Unexpected value type: {valueType}")
    };

    [Fact]
    public async Task ShouldScanWithPattern()
    {
        // Arrange
        var processInfo = GetTestProcess();
        // Attach to process first
        await _scannerService.AttachToProcessAsync(processInfo);
        var pattern = new byte[] { 0xAA, 0xBB, 0xCC };
        var mask = "xxx";

        // Act
        var result = await _scannerService.ScanForPattern(processInfo.Id, pattern, mask);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ShouldScanWithProfile()
    {
        // Arrange
        var processInfo = GetTestProcess();
        // Attach to process first
        await _scannerService.AttachToProcessAsync(processInfo);

        var profile = new ScanProfile 
        { 
            ComparisonType = "exact",
            ValueType = MemoryValueType.Int32
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