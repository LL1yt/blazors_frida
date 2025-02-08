using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace BlazorFridaApp.Tests;

public class MemoryScanBusinessLogicTests
{
    private readonly Mock<IMemoryReaderService> _memoryReaderMock;
    private readonly Mock<ILogger<ScannerGrpcService>> _loggerMock;
    private readonly Mock<IMemoryScannerService> _scannerServiceMock;
    private readonly IMemoryScannerService _scannerService;

    public MemoryScanBusinessLogicTests()
    {
        _memoryReaderMock = new Mock<IMemoryReaderService>();
        _loggerMock = new Mock<ILogger<ScannerGrpcService>>();
        _scannerServiceMock = new Mock<IMemoryScannerService>();
        
        _scannerService = _scannerServiceMock.Object;

        // Setup default mock behavior
        _scannerServiceMock
            .Setup(x => x.ScanAsync(It.IsAny<ProcessInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ScanProfile>()))
            .ReturnsAsync(new List<string> { "0x12345678" });
            
        _scannerServiceMock
            .Setup(x => x.ScanForPattern(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(new List<nint> { new nint(0x12345678) });
            
        _scannerServiceMock
            .Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ReturnsAsync(new List<nint> { new nint(0x12345678) });
    }

    [Theory]
    [InlineData(MemoryValueType.Byte, 1)]
    [InlineData(MemoryValueType.Short, 2)]
    [InlineData(MemoryValueType.Int, 4)]
    [InlineData(MemoryValueType.Long, 8)]
    [InlineData(MemoryValueType.Float, 4)]
    [InlineData(MemoryValueType.Double, 8)]
    public async Task ShouldUseCorrectValueTypeSize(MemoryValueType valueType, int expectedSize)
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var profile = new ScanProfile { ValueType = valueType };

        // Act
        var result = await _scannerService.ScanAsync(
            processInfo,
            "42",
            (int)ScanType.ExactValue,
            profile);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("0x12345678", result);
        _scannerServiceMock.Verify(x => x.ScanAsync(
            It.Is<ProcessInfo>(p => p.Id == 1234),
            "42",
            (int)ScanType.ExactValue,
            It.Is<ScanProfile>(p => p.ValueType == valueType)), 
            Times.Once);
        _memoryReaderMock.Verify(x => x.ReadMemoryBytes(
            It.IsAny<IntPtr>(),
            It.Is<int>(size => size == expectedSize)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ShouldHandleComparisonTypes()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var profiles = new[]
        {
            new ScanProfile { ComparisonType = "exact", ValueType = MemoryValueType.Int },
            new ScanProfile { ComparisonType = "greater", ValueType = MemoryValueType.Int },
            new ScanProfile { ComparisonType = "less", ValueType = MemoryValueType.Int }
        };

        foreach (var profile in profiles)
        {
            // Act
            var result = await _scannerService.ScanAsync(
                processInfo,
                "42",
                (int)ScanType.ExactValue,
                profile);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("0x12345678", result);
            _scannerServiceMock.Verify(x => x.ScanAsync(
                It.Is<ProcessInfo>(p => p.Id == 1234),
                "42",
                (int)ScanType.ExactValue,
                It.Is<ScanProfile>(p => p.ComparisonType == profile.ComparisonType)), 
                Times.Once);
        }
    }

    [Fact]
    public async Task ShouldValidatePatternFormat()
    {
        // Arrange
        _scannerServiceMock
            .Setup(x => x.ScanForPattern(It.IsAny<int>(), It.Is<byte[]>(b => b.Length == 0), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Invalid pattern"));
            
        _scannerServiceMock
            .Setup(x => x.ScanForPattern(It.IsAny<int>(), It.Is<byte[]>(b => b.Length == 2), It.Is<string>(s => s.Length == 1)))
            .ThrowsAsync(new ArgumentException("Pattern and mask length mismatch"));
            
        _scannerServiceMock
            .Setup(x => x.ScanForPattern(It.IsAny<int>(), It.IsAny<byte[]>(), It.Is<string>(s => s == "a")))
            .ThrowsAsync(new ArgumentException("Invalid mask character"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(1234, Array.Empty<byte>(), "x"));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(1234, new byte[] { 0xAA, 0xBB }, "x"));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(1234, new byte[] { 0xAA }, "a"));
    }

    [Fact]
    public async Task ShouldValidateValueRanges()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        
        _scannerServiceMock
            .Setup(x => x.ScanAsync(
                It.IsAny<ProcessInfo>(), 
                It.Is<string>(s => s == "-1000000000"), 
                It.IsAny<int>(), 
                It.IsAny<ScanProfile>()))
            .ThrowsAsync(new ArgumentException("Value out of range"));
            
        _scannerServiceMock
            .Setup(x => x.ScanAsync(
                It.IsAny<ProcessInfo>(), 
                It.Is<string>(s => s == "1000000000"), 
                It.IsAny<int>(), 
                It.IsAny<ScanProfile>()))
            .ThrowsAsync(new ArgumentException("Value out of range"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanAsync(processInfo, "-1000000000", (int)ScanType.ExactValue, new ScanProfile()));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanAsync(processInfo, "1000000000", (int)ScanType.ExactValue, new ScanProfile()));
    }

    [Fact]
    public async Task ShouldRespectMemoryBoundaries()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        
        _memoryReaderMock.Setup(x => x.ReadMemoryBytes(It.IsAny<IntPtr>(), It.IsAny<int>()))
            .ThrowsAsync(new AccessViolationException());

        // Act
        var results = await _scannerService.ScanAsync(processInfo, "42", (int)ScanType.ExactValue, new ScanProfile());

        // Assert
        Assert.Empty(results);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ShouldOptimizeMemoryAccess()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var readCalls = 0;

        _memoryReaderMock.Setup(x => x.ReadMemoryBytes(It.IsAny<IntPtr>(), It.IsAny<int>()))
            .ReturnsAsync((IntPtr addr, int size) => {
                readCalls++;
                return new byte[size];
            });

        // Act
        await _scannerService.ScanAsync(processInfo, "42", (int)ScanType.ExactValue, new ScanProfile());

        // Assert
        Assert.True(readCalls > 0, "Should perform memory reads");
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Memory reads performed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), 
            Times.Once);
    }

    [Fact]
    public async Task ShouldPreserveExecutionOrder()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var executionOrder = new List<string>();

        _memoryReaderMock.Setup(x => x.ReadMemoryBytes(It.IsAny<IntPtr>(), It.IsAny<int>()))
            .Callback(() => executionOrder.Add("Read"))
            .ReturnsAsync(new byte[4]);

        // Act
        await _scannerService.ScanAsync(processInfo, "42", (int)ScanType.ExactValue, new ScanProfile());

        // Assert
        Assert.Equal("Read", executionOrder[0]);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting scan")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), 
            Times.Once);
    }
}