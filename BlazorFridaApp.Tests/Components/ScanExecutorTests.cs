using Bunit;
using Bunit.Rendering;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.Generic;
using Blazorise;
using Microsoft.AspNetCore.Components;
using BlazorFridaApp.MemoryScanner.Base;

namespace BlazorFridaApp.Tests.Components;

public class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> LogEntries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add((logLevel, formatter(state, exception), exception));
    }
}

public class ScanExecutorTests : TestContextBase
{
    private readonly Mock<IMemoryScannerService> _scannerServiceMock;
    private readonly Mock<ILogger<ScanExecutor>> _loggerMock;
    private readonly Mock<BlazorFridaApp.Services.INotificationService> _notificationServiceMock;
    private readonly Mock<IProcessMemoryScanner> _processMemoryScannerMock;

    public ScanExecutorTests()
    {
        _scannerServiceMock = new Mock<IMemoryScannerService>();
        _loggerMock = new Mock<ILogger<ScanExecutor>>();
        _notificationServiceMock = new Mock<BlazorFridaApp.Services.INotificationService>();
        _processMemoryScannerMock = new Mock<IProcessMemoryScanner>();
        
        Services.AddScoped<IMemoryScannerService>(_ => _scannerServiceMock.Object);
        Services.AddScoped<ILogger<ScanExecutor>>(_ => _loggerMock.Object);
        Services.AddScoped<ILogger<MemoryScannerComponentBase>>(_ => Mock.Of<ILogger<MemoryScannerComponentBase>>());
        Services.AddScoped<BlazorFridaApp.Services.INotificationService>(_ => _notificationServiceMock.Object);
        Services.AddScoped<IProcessMemoryScanner>(_ => _processMemoryScannerMock.Object);
    }

    [Fact]
    public async Task ShouldHandleEmptyScanResults()
    {
        // Arrange
        var processId = 1234;
        var value = 42;

        _scannerServiceMock.Setup(x => x.ScanForValue(processId, value, MemoryValueType.Int32))
            .ReturnsAsync(new List<IntPtr>());

        // Act
        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, processId)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.IsFirstScan, true));

        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => value));

        // Assert
        _scannerServiceMock.Verify(x => x.ScanForValue(processId, value, MemoryValueType.Int32), Times.Once);
    }

    [Fact]
    public async Task ShouldLogErrorWhenScanningFails()
    {
        // Arrange
        var testException = new Exception("Test error");
        _scannerServiceMock.Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ThrowsAsync(testException);

        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.IsFirstScan, true));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 42));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldShowNotificationWhenScanningFails()
    {
        // Arrange
        var testException = new Exception("Test error");
        _scannerServiceMock.Setup(x => x.ScanForValue(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MemoryValueType>()))
            .ThrowsAsync(testException);

        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.IsFirstScan, true));

        // Act
        await cut.InvokeAsync(() => cut.Instance.ExecuteScan(_ => 42));

        // Assert
        _notificationServiceMock.Verify(x => 
            x.ShowError("Scan failed", "Test error", It.IsAny<Exception?>()),
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
        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ValueType, MemoryValueType.Int32)
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

        var cut = RenderComponent<ScanExecutor>(parameters => parameters
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
        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, 1000)
            .Add(p => p.ValueType, MemoryValueType.Int32)
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
        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.ProcessId, null)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        Assert.False(cut.Instance.CanExecuteScan);
    }

    [Fact]
    public void ShouldShowProgressBarWhenLoading()
    {
        // Arrange & Act
        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.IsLoading, true));

        // Assert
        var progressBar = cut.FindComponent<Progress>();
        Assert.NotNull(progressBar);
    }

    [Fact]
    public void ShouldNotShowProgressBarWhenNotLoading()
    {
        // Arrange & Act
        var cut = RenderComponent<ScanExecutor>(parameters => parameters
            .Add(p => p.IsLoading, false));

        // Assert
        Assert.Throws<ComponentNotFoundException>(() => cut.FindComponent<Progress>());
    }
}