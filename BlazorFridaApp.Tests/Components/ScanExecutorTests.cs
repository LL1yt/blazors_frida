using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.Generic;
using Radzen;
using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Base;

namespace BlazorFridaApp.Tests.Components;

public class ScanExecutorTests : BunitContext
{
    private readonly Mock<IMemoryScannerService> _scannerServiceMock;
    private readonly Mock<ILogger<ScanExecutor>> _loggerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IProcessMemoryScanner> _processMemoryScannerMock;

    public ScanExecutorTests()
    {
        _scannerServiceMock = new Mock<IMemoryScannerService>();
        _loggerMock = new Mock<ILogger<ScanExecutor>>();
        _notificationServiceMock = new Mock<INotificationService>();
        _processMemoryScannerMock = new Mock<IProcessMemoryScanner>();
        
        Services.AddScoped<IMemoryScannerService>(_ => _scannerServiceMock.Object);
        Services.AddScoped<ILogger<ScanExecutor>>(_ => _loggerMock.Object);
        Services.AddScoped<ILogger<MemoryScannerComponentBase>>(_ => Mock.Of<ILogger<MemoryScannerComponentBase>>());
        Services.AddScoped<INotificationService>(_ => _notificationServiceMock.Object);
        Services.AddScoped<IProcessMemoryScanner>(_ => _processMemoryScannerMock.Object);
    }

    [Fact]
    public async Task ShouldHandleEmptyScanResults()
    {
        // Arrange
        _scannerServiceMock.Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ReturnsAsync(new List<nint>());

        var cut = Render<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.SearchValue, 42)
            .Add(p => p.IsFirstScan, true));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 42));

        // Assert
        _notificationServiceMock.Verify(x => 
            x.ShowInfo(It.Is<string>(s => s.Contains("Scan Results")), 
                      It.Is<string>(s => s.Contains("No results found"))), 
            Times.Once);
    }

    [Fact]
    public async Task ShouldHandleExceptionsDuringScanning()
    {
        // Arrange
        _scannerServiceMock.Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ThrowsAsync(new Exception("Test error"));

        var cut = Render<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.SearchValue, 42)
            .Add(p => p.IsFirstScan, true));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 42));

        // Assert
        _loggerMock.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error during memory scan")),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
        Times.Once());

        _notificationServiceMock.Verify(x => 
            x.ShowError(It.Is<string>(s => s.Contains("Scan failed")), 
                       It.Is<string>(s => s.Contains("Test error"))),
            Times.Once);
    }

    [Fact]
    public async Task ShouldHandleLargeResults()
    {
        // Arrange
        var largeResults = new List<nint>();
        for (var i = 0; i < 10000; i++)
        {
            largeResults.Add(new IntPtr(i));
        }

        _scannerServiceMock.Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ReturnsAsync(largeResults);

        var resultCount = 0;
        var cut = Render<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.SearchValue, 42)
            .Add(p => p.IsFirstScan, true)
            .Add(p => p.OnScanComplete, EventCallback.Factory.Create<List<IntPtr>>(this, results =>
            {
                resultCount = results.Count;
                return Task.CompletedTask;
            })));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 42));

        // Assert
        Assert.Equal(10000, resultCount);
    }

    [Fact]
    public async Task ShouldHandlePatternScanCorrectly()
    {
        // Arrange
        var pattern = new byte[] { 0xAA, 0xBB, 0xCC };
        var mask = "xxx";
        var results = new List<nint> { new IntPtr(0x1000) };

        _scannerServiceMock.Setup(x => x.ScanForPattern(It.IsAny<int>(), pattern, mask))
            .ReturnsAsync(results);

        var cut = Render<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ScanType, ScanType.Pattern)
            .Add(p => p.PatternHex, "AA BB CC")
            .Add(p => p.Mask, mask)
            .Add(p => p.IsFirstScan, true));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 0));

        // Assert
        _scannerServiceMock.Verify(x => x.ScanForPattern(1000, pattern, mask), Times.Once);
    }

    [Fact]
    public async Task ShouldUpdateLoadingState()
    {
        // Arrange
        _scannerServiceMock.Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ReturnsAsync(new List<nint>());
            
        var loadingStates = new List<bool>();
        var cut = Render<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.SearchValue, 42)
            .Add(p => p.IsFirstScan, true)
            .Add(p => p.OnLoadingChanged, EventCallback.Factory.Create<bool>(this, isLoading =>
            {
                loadingStates.Add(isLoading);
                return Task.CompletedTask;
            })));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 42));

        // Assert
        Assert.Equal(2, loadingStates.Count); // Should be true then false
        Assert.True(loadingStates[0]); // Started loading
        Assert.False(loadingStates[1]); // Finished loading
    }

    [Fact]
    public void ShouldRequireValidProcessId()
    {
        // Arrange & Act
        var cut = Render<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, null)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        Assert.False(cut.Instance.CanExecuteScan);
    }
}