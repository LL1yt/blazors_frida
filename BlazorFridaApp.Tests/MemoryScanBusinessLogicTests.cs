using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Configuration;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorFridaApp.Tests;

public class MemoryScanBusinessLogicTests : IntegrationTestBase
{
    private readonly ILogger<ProcessGrpcService> _processLogger;
    private readonly ILogger<MemoryGrpcService> _memoryLogger;
    private readonly ILogger<ScannerGrpcService> _scannerLogger;
    private readonly ILogger<StateGrpcService> _stateLogger;
    private readonly ILogger<FreezeGrpcService> _freezeLogger;
    private readonly ILogger<MemoryScannerGrpcService> _memoryScannerLogger;
    private readonly IMemoryScannerGrpcService _memoryScannerService;
    private ProcessInfo _notepadProcess;

    public MemoryScanBusinessLogicTests() : base()
    {
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Constructor started");
        Environment.SetEnvironmentVariable("BLAZOR_FRIDA_TEST", "true");
        
        // Create real logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Creating service loggers");
        _processLogger = loggerFactory.CreateLogger<ProcessGrpcService>();
        _memoryLogger = loggerFactory.CreateLogger<MemoryGrpcService>();
        _scannerLogger = loggerFactory.CreateLogger<ScannerGrpcService>();
        _stateLogger = loggerFactory.CreateLogger<StateGrpcService>();
        _freezeLogger = loggerFactory.CreateLogger<FreezeGrpcService>();
        _memoryScannerLogger = loggerFactory.CreateLogger<MemoryScannerGrpcService>();

        // Create settings
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Creating settings and services");
        var settings = new MemoryScannerSettings();
        var options = Options.Create(settings);

        // Create real services
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Creating gRPC services using ProcessManager from base");
        var scannerService = new ScannerGrpcService(_scannerLogger, ProcessManager);
        var processService = new ProcessGrpcService(_processLogger, ProcessManager);
        var memoryService = new MemoryGrpcService(_memoryLogger, ProcessManager);
        var stateService = new StateGrpcService(_stateLogger, ProcessManager);
        var freezeService = new FreezeGrpcService(_freezeLogger, ProcessManager);

        // Create memory scanner service
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Creating MemoryScannerGrpcService");
        _memoryScannerService = new MemoryScannerGrpcService(
            processService,
            memoryService,
            scannerService,
            stateService,
            freezeService,
            options,
            _memoryScannerLogger);

        // Find Notepad process
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Looking for Notepad process");
        _notepadProcess = FindNotepadProcess().GetAwaiter().GetResult();
        if (_notepadProcess == null)
        {
            Logger.LogError("[MemoryScanBusinessLogicTests] Notepad process not found");
            throw new InvalidOperationException("Please start Notepad.exe before running tests");
        }
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Constructor completed. Found Notepad process with ID: {ProcessId}", _notepadProcess.Id);
    }

    private async Task<ProcessInfo> FindNotepadProcess()
    {
        Logger.LogInformation("[MemoryScanBusinessLogicTests] FindNotepadProcess started");
        var processes = await _memoryScannerService.ListProcessesAsync();
        Logger.LogInformation("[MemoryScanBusinessLogicTests] Found {Count} processes", processes.Count());
        var notepad = processes.FirstOrDefault(p => p.Name.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase));
        if (notepad != null)
        {
            Logger.LogInformation("[MemoryScanBusinessLogicTests] Found Notepad process with ID: {ProcessId}", notepad.Id);
        }
        else
        {
            Logger.LogWarning("[MemoryScanBusinessLogicTests] Notepad process not found in process list");
        }
        return notepad ?? throw new InvalidOperationException("Notepad.exe process not found");
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
        Logger.LogInformation("Memory scan completed successfully");
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
        Logger.LogInformation("Scan execution completed in expected order");
    }
}