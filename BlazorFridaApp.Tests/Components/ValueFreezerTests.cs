using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Base;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

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
        
        Services
            .AddBlazorise()
            .AddBootstrap5Providers()
            .AddFontAwesomeIcons();
            
        // Add style provider
        JSInterop.SetupModule("_content/Blazorise/blazorise.js");
        JSInterop.SetupModule("_content/Blazorise.Bootstrap5/blazorise.bootstrap5.js");
    }

    [Fact]
    public void ShouldRenderNumericInput()
    {
        // Arrange & Act
        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var numericEdit = cut.FindComponent<NumericEdit<int>>();
        Assert.NotNull(numericEdit);
    }

    [Fact]
    public void ShouldRenderFreezeButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ValueFreezer>(parameters => parameters
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var button = cut.FindComponent<Button>();
        Assert.NotNull(button);
    }
}