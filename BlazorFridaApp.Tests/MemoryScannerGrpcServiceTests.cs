using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorFridaApp.Tests.Helpers;
using Grpc.Net.Client;
using Grpc.Core;

namespace BlazorFridaApp.Tests;

public class MemoryScannerGrpcServiceTests : IDisposable
{
    private readonly Mock<ILogger<MemoryScannerFacade>> _loggerMock;
    private readonly Mock<ProcessGrpcService> _processServiceMock;
    private readonly Mock<MemoryGrpcService> _memoryServiceMock;
    private readonly Mock<ScannerGrpcService> _scannerServiceMock;
    private readonly Mock<StateGrpcService> _stateServiceMock;
    private readonly Mock<FreezeGrpcService> _freezeServiceMock;
    private readonly IMemoryScannerGrpcService _service;

    public MemoryScannerGrpcServiceTests()
    {
        _loggerMock = new Mock<ILogger<MemoryScannerFacade>>();
        _processServiceMock = new Mock<ProcessGrpcService>();
        _memoryServiceMock = new Mock<MemoryGrpcService>();
        _scannerServiceMock = new Mock<ScannerGrpcService>();
        _stateServiceMock = new Mock<StateGrpcService>();
        _freezeServiceMock = new Mock<FreezeGrpcService>();
        
        SetupMockResponses();
        
        _service = new MemoryScannerFacade(
            _processServiceMock.Object,
            _memoryServiceMock.Object,
            _scannerServiceMock.Object,
            _stateServiceMock.Object,
            _freezeServiceMock.Object,
            _loggerMock.Object
        );
    }

    private void SetupMockResponses()
    {
        // Setup mock responses for individual services
        _processServiceMock.Setup(x => x.ListProcessesAsync())
            .ReturnsAsync(new List<ProcessInfo> { new ProcessInfo(1234, "test.exe") });
            
        _processServiceMock.Setup(x => x.AttachToProcessAsync(It.IsAny<int>()))
            .ReturnsAsync(("test-session", true));
            
        _memoryServiceMock.Setup(x => x.ReadMemoryAsync(
            It.IsAny<string>(),
            It.IsAny<ulong>(),
            It.IsAny<int>(),
            It.IsAny<string>()
        )).ReturnsAsync((new byte[] { 1, 2, 3, 4 }, true, string.Empty));
        
        // Setup WriteMemory mock response
        _memoryServiceMock.Setup(x => x.WriteMemoryAsync(
            It.IsAny<string>(),
            It.IsAny<ulong>(),
            It.IsAny<byte[]>(),
            It.IsAny<string>()
        )).ReturnsAsync((true, string.Empty));
        
        // Setup ScanMemory mock response
        _scannerServiceMock.Setup(x => x.ScanMemoryAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<string>(),
            It.IsAny<List<(ulong start, ulong end)>>()
        )).ReturnsAsync(new List<ScanResult> { new ScanResult { Addresses = new List<nint> { 0x1000 } } });
        
        // Setup GetState mock response
        _stateServiceMock.Setup(x => x.GetStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        )).ReturnsAsync((new Dictionary<string, byte[]> { { "key1", new byte[] { 1, 2, 3 } } }, "test-version"));
        
        // Setup SyncState mock response
        _stateServiceMock.Setup(x => x.SyncStateAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, byte[]>>(),
            It.IsAny<string>()
        )).ReturnsAsync((true, string.Empty, "test-version-2"));
    }

    [Fact]
    public async Task ReadMemoryAsync_ShouldHandleSuccessfulRead()
    {
        // Arrange
        const string sessionId = "test-session";
        const ulong address = 0x1000;
        const int size = 4;
        const string valueType = "int32";

        // Act
        var result = await _service.ReadMemoryAsync(sessionId, address, size, valueType);

        // Assert
        Assert.NotNull(result.value);
        Assert.True(result.success);
        Assert.Empty(result.error);
    }

    [Fact]
    public async Task WriteMemoryAsync_ShouldHandleSuccessfulWrite()
    {
        // Arrange
        const string sessionId = "test-session";
        const ulong address = 0x1000;
        var value = new byte[] { 1, 2, 3, 4 };
        const string valueType = "int32";

        // Act
        var result = await _service.WriteMemoryAsync(sessionId, address, value, valueType);

        // Assert
        Assert.True(result.success);
        Assert.Empty(result.error);
    }

    [Fact]
    public async Task ScanMemoryAsync_ShouldHandleValidScan()
    {
        // Arrange
        const string sessionId = "test-session";
        const string valueType = "int32";
        var value = new byte[] { 1, 2, 3, 4 };
        const string comparisonType = "exact";
        var ranges = new List<(ulong start, ulong end)> 
        { 
            (0x1000, 0x2000) 
        };

        // Act
        var results = await _service.ScanMemoryAsync(sessionId, valueType, value, comparisonType, ranges);

        // Assert
        Assert.NotNull(results);
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.Equal(1, resultsList[0].Addresses.Count);
        Assert.Equal(new nint(0x1000), resultsList[0].Addresses[0]);
    }

    [Fact]
    public async Task GetStateAsync_ShouldRetrieveState()
    {
        // Arrange
        const string sessionId = "test-session";
        const string checkpointId = "checkpoint-1";

        // Act
        var result = await _service.GetStateAsync(sessionId, checkpointId);

        // Assert
        Assert.NotNull(result.state);
        Assert.NotNull(result.version);
        Assert.Single(result.state);
        Assert.Equal("test-version", result.version);
    }

    [Fact]
    public async Task SyncStateAsync_ShouldSynchronizeState()
    {
        // Arrange
        const string sessionId = "test-session";
        var stateUpdates = new Dictionary<string, byte[]>
        {
            { "key1", new byte[] { 1, 2, 3 } }
        };
        const string version = "v1";

        // Act
        var result = await _service.SyncStateAsync(sessionId, stateUpdates, version);

        // Assert
        Assert.True(result.success);
        Assert.Empty(result.error);
        Assert.Equal("test-version-2", result.newVersion);
    }

    [Fact]
    public async Task ShouldHandleServerUnavailableError()
    {
        // Arrange
        _processServiceMock.Setup(x => x.ListProcessesAsync())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "Server unavailable")));

        // Act & Assert
        var result = await _service.ReadMemoryAsync("test", 0x1000, 4, "int32");
        Assert.False(result.success);
        Assert.Contains("Server unavailable", result.error);
    }

    public void Dispose()
    {
        _service.Dispose();
    }
}