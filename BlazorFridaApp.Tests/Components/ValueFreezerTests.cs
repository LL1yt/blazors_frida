using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;
using Radzen;
using System.Collections.Generic;
using BlazorFridaApp.MemoryScanner.Base;

namespace BlazorFridaApp.Tests.Components;

public class ValueFreezerTests : TestContextWrapper, IAsyncLifetime
{
    private readonly Mock<IValueFreezerService> _freezerServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IProcessMemoryScanner> _scannerMock;

    public ValueFreezerTests()
    {
        _freezerServiceMock = new Mock<IValueFreezerService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _scannerMock = new Mock<IProcessMemoryScanner>();
        
        // Configure async disposal
        _freezerServiceMock.Setup(x => x.DisposeAsync())
            .Returns(new ValueTask());
        
        Services.AddScoped<IValueFreezerService>(_ => _freezerServiceMock.Object);
        Services.AddScoped<INotificationService>(_ => _notificationServiceMock.Object);
        Services.AddScoped<IProcessMemoryScanner>(_ => _scannerMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    [Fact]
    public async Task ShouldRenderWithoutErrors()
    {
        // Act
        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        Assert.NotNull(cut.Instance);
    }

    [Fact]
    public async Task ShouldHandleFreezeValueSuccess()
    {
        // Arrange
        var address = new IntPtr(0x1000);
        var bytes = new byte[] { 1, 2, 3, 4 };
        _freezerServiceMock.Setup(x => x.FreezeValue(address, bytes, "Int32"))
            .Returns(Task.CompletedTask);

        var cut = Render<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Act
        await cut.Instance.OnValueChanged(address, bytes);

        // Assert
        _freezerServiceMock.Verify(x => x.FreezeValue(address, bytes, "Int32"), Times.Once);
    }

    [Fact]
    public async Task ShouldHandleFreezeValueError()
    {
        // Arrange
        var address = new IntPtr(0x1000);
        var bytes = new byte[] { 1, 2, 3, 4 };
        _freezerServiceMock.Setup(x => x.FreezeValue(address, bytes, "Int32"))
            .ThrowsAsync(new Exception("Test error"));

        var cut = Render<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Act
        await cut.Instance.OnValueChanged(address, bytes);

        // Assert
        _notificationServiceMock.Verify(x => x.ShowError(
            "Failed to update frozen value",
            "Test error",
            It.IsAny<Exception>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldTrackFrozenAddresses()
    {
        // Arrange
        var address = new IntPtr(0x1000);
        var bytes = new byte[] { 1, 2, 3, 4 };
        _freezerServiceMock.Setup(x => x.GetFrozenAddresses())
            .ReturnsAsync(new HashSet<IntPtr> { address });

        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Act
        var result = cut.Instance.IsFrozen(address);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldHandleUnfreezeValue()
    {
        // Arrange
        var address = new IntPtr(0x1000);
        _freezerServiceMock.Setup(x => x.UnfreezeValue(address))
            .Returns(Task.CompletedTask);

        var cut = Render<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Act
        await cut.Instance.UnfreezeValue(address);

        // Assert
        _freezerServiceMock.Verify(x => x.UnfreezeValue(address), Times.Once);
    }

    [Fact]
    public async Task ShouldHandleUnfreezeValueError()
    {
        // Arrange
        var address = new IntPtr(0x1000);
        _freezerServiceMock.Setup(x => x.UnfreezeValue(address))
            .ThrowsAsync(new Exception("Test error"));

        var cut = Render<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Act
        await cut.Instance.UnfreezeValue(address);

        // Assert
        _notificationServiceMock.Verify(x => x.ShowError(
            "Failed to unfreeze value",
            "Test error",
            It.IsAny<Exception>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldHandleFreezeStateChange()
    {
        // Arrange
        var address = new IntPtr(0x1000);
        var bytes = new byte[] { 1, 2, 3, 4 };
        var stateChanged = false;

        var cut = Render<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.OnFreezeStateChanged, EventCallback.Factory.Create<(IntPtr, bool)>(this, state =>
            {
                stateChanged = true;
                return Task.CompletedTask;
            })));

        // Act
        await cut.Instance.OnValueChanged(address, bytes);

        // Assert
        Assert.True(stateChanged);
    }

    [Fact]
    public async Task ShouldRenderFreezeControls()
    {
        // Arrange & Act
        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var controls = cut.FindAll(".freeze-control");
        Assert.NotEmpty(controls);
    }
}