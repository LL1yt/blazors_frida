using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorFridaApp.Tests.Helpers;
using Grpc.Net.Client;
using Grpc.Core;

namespace BlazorFridaApp.Tests;

public class MemoryScannerGrpcServiceTests : IDisposable
{
    private readonly Mock<ILogger<MemoryScannerGrpcService>> _loggerMock;
    private readonly Mock<IPythonProcessManager> _processManagerMock;
    private readonly MemoryScannerGrpcService _service;

    public MemoryScannerGrpcServiceTests()
    {
        _loggerMock = new Mock<ILogger<MemoryScannerGrpcService>>();
        _processManagerMock = new Mock<IPythonProcessManager>();
        _processManagerMock.Setup(x => x.Port).Returns(50051);
        _processManagerMock.Setup(x => x.EnsureServerRunning()).Returns(Task.CompletedTask);
        
        _service = new MemoryScannerGrpcService(_loggerMock.Object, _processManagerMock.Object);
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
        _processManagerMock.Verify(x => x.EnsureServerRunning(), Times.Once);
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
        _processManagerMock.Verify(x => x.EnsureServerRunning(), Times.Once);
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
        _processManagerMock.Verify(x => x.EnsureServerRunning(), Times.Once);
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
        _processManagerMock.Verify(x => x.EnsureServerRunning(), Times.Once);
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
        _processManagerMock.Verify(x => x.EnsureServerRunning(), Times.Once);
    }

    [Fact]
    public async Task ShouldHandleServerUnavailableError()
    {
        // Arrange
        _processManagerMock.Setup(x => x.EnsureServerRunning())
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