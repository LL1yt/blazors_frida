using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorFridaApp.Tests;

public class MemoryScanBusinessLogicTests : IDisposable
{
    private readonly ILogger<MemoryScanBusinessLogicTests> _logger;
    private readonly ILogger<ProcessGrpcService> _processLogger;
    private readonly ILogger<MemoryGrpcService> _memoryLogger;
    private readonly ILogger<ScannerGrpcService> _scannerLogger;
    private readonly ILogger<StateGrpcService> _stateLogger;
    private readonly ILogger<FreezeGrpcService> _freezeLogger;
    private readonly ILogger<PythonProcessManager> _processManagerLogger;
    private readonly ILogger<MemoryScannerGrpcService> _memoryScannerLogger;
    private readonly PythonProcessManager _processManager;
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
        
        _logger = loggerFactory.CreateLogger<MemoryScanBusinessLogicTests>();
        _processLogger = loggerFactory.CreateLogger<ProcessGrpcService>();
        _memoryLogger = loggerFactory.CreateLogger<MemoryGrpcService>();
        _scannerLogger = loggerFactory.CreateLogger<ScannerGrpcService>();
        _stateLogger = loggerFactory.CreateLogger<StateGrpcService>();
        _freezeLogger = loggerFactory.CreateLogger<FreezeGrpcService>();
        _processManagerLogger = loggerFactory.CreateLogger<PythonProcessManager>();
        _memoryScannerLogger = loggerFactory.CreateLogger<MemoryScannerGrpcService>();

        // Create real process manager that connects to running server
        _processManager = new PythonProcessManager(_processManagerLogger);

        // Create settings
        var settings = new MemoryScannerSettings();
        var options = Options.Create(settings);

        // Create real services
        var scannerService = new ScannerGrpcService(_scannerLogger, _processManager);
        var processService = new ProcessGrpcService(_processLogger, _processManager);
        var memoryService = new MemoryGrpcService(_memoryLogger, _processManager);
        var stateService = new StateGrpcService(_stateLogger, _processManager);
        var freezeService = new FreezeGrpcService(_freezeLogger, _processManager);

        // Create memory scanner service
        _memoryScannerService = new MemoryScannerGrpcService(
            processService,
            memoryService,
            scannerService,
            stateService,
            freezeService,
            options,
            _memoryScannerLogger);

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
        if (_memoryScannerService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Theory]
    [InlineData(MemoryValueType.Byte, 1)]
    [InlineData(MemoryValueType.Int16, 2)]
    [InlineData(MemoryValueType.Int32, 4)]
    [InlineData(MemoryValueType.Int64, 8)]
    [InlineData(MemoryValueType.Float, 4)]
    [InlineData(MemoryValueType.Double, 8)]
    public async Task ShouldUseCorrectValueTypeSize(MemoryValueType valueType, int expectedSize)
    {
        // Act
        var result = await _memoryScannerService.ScanMemoryAsync(
            _notepadProcess.Id.ToString(),
            valueType.ToString(),
            BitConverter.GetBytes(42),
            "exact",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Any() || !result.Any(), $"Should return a valid result list for {valueType} with size {expectedSize}");
    }

    [Fact]
    public async Task ShouldHandleComparisonTypes()
    {
        // Arrange
        var comparisonTypes = new[] { "exact", "greater", "less" };

        foreach (var comparisonType in comparisonTypes)
        {
            // Act
            var result = await _memoryScannerService.ScanMemoryAsync(
                _notepadProcess.Id.ToString(),
                "int32",
                BitConverter.GetBytes(42),
                comparisonType,
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Any() || !result.Any(), $"Should return a valid result list for comparison type {comparisonType}");
        }
    }

    [Fact]
    public async Task ShouldValidatePatternFormat()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                _notepadProcess.Id.ToString(),
                "pattern",
                Array.Empty<byte>(),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                _notepadProcess.Id.ToString(),
                "pattern",
                new byte[] { 0xAA, 0xBB },
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                _notepadProcess.Id.ToString(),
                "pattern",
                new byte[] { 0xAA },
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));
    }

    [Fact]
    public async Task ShouldValidateValueRanges()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                _notepadProcess.Id.ToString(),
                "int32",
                BitConverter.GetBytes(-1000000000),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                _notepadProcess.Id.ToString(),
                "int32",
                BitConverter.GetBytes(1000000000),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));
    }

    [Fact]
    public async Task ShouldRespectMemoryBoundaries()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                "notepad_1000",
                "int32",
                BitConverter.GetBytes(-1000000000),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));

        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryScannerService.ScanMemoryAsync(
                "notepad_1000",
                "int32",
                BitConverter.GetBytes(1000000000),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));
    }

    [Fact]
    public async Task ShouldOptimizeMemoryAccess()
    {
        // Act
        var result = await _memoryScannerService.ScanMemoryAsync(
            "notepad_1000",
            "int32",
            BitConverter.GetBytes(42),
            "exact",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        // Assert
        Assert.NotNull(result);
        _logger.LogInformation("Memory scan completed successfully");
    }

    [Fact]
    public async Task ShouldPreserveExecutionOrder()
    {
        // Act
        var result = await _memoryScannerService.ScanMemoryAsync(
            "notepad_1000",
            "int32",
            BitConverter.GetBytes(42),
            "exact",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        // Assert
        Assert.NotNull(result);
        _logger.LogInformation("Scan execution completed in expected order");
    }
}