using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Base;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BlazorFridaApp.Tests.Components;

public class ValueFreezerTests : TestContextBase
{
    private readonly Mock<IValueFreezerService> _freezerMock;
    private readonly Mock<IProcessMemoryScanner> _scannerMock;

    public ValueFreezerTests()
    {
        _freezerMock = new Mock<IValueFreezerService>();
        _scannerMock = new Mock<IProcessMemoryScanner>();
        
        Services.AddSingleton(_freezerMock.Object);
        Services.AddSingleton(_scannerMock.Object);
    }

    [Fact]
    public void ShouldRenderNumericInput()
    {
        // Arrange & Act
        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var numericInput = cut.Find("input[type='number']");
        Assert.NotNull(numericInput);
    }

    [Fact]
    public void ShouldRenderFreezeButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var button = cut.Find("button");
        Assert.NotNull(button);
        Assert.Equal("Freeze", button.TextContent.Trim());
    }
}