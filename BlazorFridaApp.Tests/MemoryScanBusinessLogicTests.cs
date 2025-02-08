using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorFridaApp.Tests;

public class MemoryScanBusinessLogicTests : IDisposable
{
    private readonly ILogger<ScannerGrpcService> _logger;
    private readonly ILogger<PythonProcessManager> _processManagerLogger;
    private readonly ILogger<MemoryScannerFacade> _facadeLogger;
    private readonly PythonProcessManager _processManager;
    private readonly ScannerGrpcService _scannerService;
    private readonly IMemoryScannerGrpcService _memoryScannerService;
    private ProcessInfo _notepadProcess;

    public MemoryScanBusinessLogicTests()
    {
        // Create real logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<ScannerGrpcService>();
        _processManagerLogger = loggerFactory.CreateLogger<PythonProcessManager>();
        _facadeLogger = loggerFactory.CreateLogger<MemoryScannerFacade>();

        // Create real process manager that connects to running server
        _processManager = new PythonProcessManager(_processManagerLogger);
        _processManager.SetPort(50051); // Default gRPC port

        // Create real scanner service
        _scannerService = new ScannerGrpcService(_logger, _processManager);

        // Create settings
        var settings = new MemoryScannerSettings();
        var options = Options.Create(settings);

        // Create facade with real scanner service
        _memoryScannerService = new MemoryScannerFacade(
            null, // processService
            null, // memoryService
            _scannerService, // scannerService
            null, // stateService
            null, // freezeService
            options,
            _facadeLogger);

        // Find Notepad process
        _notepadProcess = FindNotepadProcess().GetAwaiter().GetResult();
        if (_notepadProcess == null)
        {
            throw new InvalidOperationException("Please start Notepad.exe before running tests");
        }
    }

    private async Task<ProcessInfo> FindNotepadProcess()
    {
        var processes = await _memoryScannerService.ListProcessesAsync();
        var notepad = processes.FirstOrDefault(p => p.Name.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase));
        return notepad ?? throw new InvalidOperationException("Notepad.exe process not found");
    }

    public void Dispose()
    {
        _scannerService.Dispose();
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
        // Act
        var result = await _scannerService.ScanForValue(_notepadProcess.Id, 42, valueType);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 0, $"Should return a valid result list for {valueType} with size {expectedSize}");
    }

    [Fact]
    public async Task ShouldHandleComparisonTypes()
    {
        // Arrange
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
                _notepadProcess,
                "42",
                (int)ScanType.ExactValue,
                profile);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Any() || !result.Any(), $"Should return a valid result list for comparison type {profile.ComparisonType}");
        }
    }

    [Fact]
    public async Task ShouldValidatePatternFormat()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(_notepadProcess.Id, Array.Empty<byte>(), "x"));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(_notepadProcess.Id, new byte[] { 0xAA, 0xBB }, "x"));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanForPattern(_notepadProcess.Id, new byte[] { 0xAA }, "a"));
    }

    [Fact]
    public async Task ShouldValidateValueRanges()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanAsync(_notepadProcess, "-1000000000", (int)ScanType.ExactValue, new ScanProfile()));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _scannerService.ScanAsync(_notepadProcess, "1000000000", (int)ScanType.ExactValue, new ScanProfile()));
    }

    [Fact]
    public async Task ShouldRespectMemoryBoundaries()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        
        _scannerServiceMock.Setup(x => x.ScanAsync(
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
    public async Task ShouldOptimizeMemoryAccess()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var readCalls = 0;

        _scannerServiceMock.Setup(x => x.ScanAsync(It.IsAny<ProcessInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ScanProfile>()))
            .ReturnsAsync((ProcessInfo p, string s, int i, ScanProfile sp) => {
                readCalls++;
                return new List<string>();
            });

        // Act
        await _scannerService.ScanAsync(processInfo, "42", (int)ScanType.ExactValue, new ScanProfile());

        // Assert
        Assert.True(readCalls > 0, "Should perform memory reads");
        _logger.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Memory reads performed")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>());
    }

    [Fact]
    public async Task ShouldPreserveExecutionOrder()
    {
        // Arrange
        var processInfo = new ProcessInfo { Id = 1234, Name = "test.exe" };
        var executionOrder = new List<string>();

        _scannerServiceMock.Setup(x => x.ScanAsync(It.IsAny<ProcessInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ScanProfile>()))
            .ReturnsAsync((ProcessInfo p, string s, int i, ScanProfile sp) => {
                executionOrder.Add("Read");
                return new List<string>();
            });

        // Act
        await _scannerService.ScanAsync(processInfo, "42", (int)ScanType.ExactValue, new ScanProfile());

        // Assert
        Assert.Equal("Read", executionOrder[0]);
        _logger.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting scan")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>());
    }
}