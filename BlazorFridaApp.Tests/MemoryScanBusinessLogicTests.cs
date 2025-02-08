using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorFridaApp.Tests;

public class MemoryScanBusinessLogicTests
{
    private readonly Mock<IMemoryReaderService> _memoryReaderMock;
    private readonly Mock<ILogger<ScannerGrpcService>> _loggerMock;
    private readonly Mock<IPythonProcessManager> _processManagerMock;
    private readonly IMemoryScannerService _scannerService;

    public MemoryScanBusinessLogicTests()
    {
        _memoryReaderMock = new Mock<IMemoryReaderService>();
        _loggerMock = new Mock<ILogger<ScannerGrpcService>>();
        _processManagerMock = new Mock<IPythonProcessManager>();
        
        _scannerService = new ScannerGrpcService(
            _loggerMock.Object,
            _processManagerMock.Object);
    }

    [Fact]
    public async Task ShouldValidateValueRanges()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        
        // Act & Assert - Test minimum value
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanAsync(processInfo, "-1000000000", (int)ScanType.ExactValue, new ScanProfile()));

        // Act & Assert - Test maximum value
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanAsync(processInfo, "1000000000", (int)ScanType.ExactValue, new ScanProfile()));
    }

    [Fact]
    public async Task ShouldValidatePatternFormat()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        
        // Invalid hex pattern
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(1234, new byte[] { }, "x"));

        // Pattern and mask length mismatch
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(1234, new byte[] { 0xAA, 0xBB }, "x"));

        // Invalid mask characters
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(1234, new byte[] { 0xAA }, "a"));
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
        var address = new IntPtr(0x1000);
        var value = new byte[expectedSize];
        
        _memoryReaderMock.Setup(x => x.ReadMemoryBytes(address, expectedSize))
            .ReturnsAsync(value);

        // Act
        await _scannerService.ScanForValue(1234, 42, valueType);

        // Assert
        _memoryReaderMock.Verify(x => x.ReadMemoryBytes(It.IsAny<IntPtr>(), expectedSize), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ShouldHandleComparisonTypes()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var profile = new ScanProfile { ComparisonType = "exact" };

        _memoryReaderMock.Setup(x => x.ReadMemoryBytes(It.IsAny<IntPtr>(), It.IsAny<int>()))
            .ReturnsAsync(BitConverter.GetBytes(42));

        // Act
        await _scannerService.ScanAsync(processInfo, "42", (int)ScanType.ExactValue, profile);

        // Assert
        _memoryReaderMock.Verify(x => x.ReadMemoryBytes(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.AtLeastOnce());
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