using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Blazorise;

namespace BlazorFridaApp.Tests.Components;

public class ValueFreezerTests : TestContextBase
{
    public ValueFreezerTests()
    {
        // Add Blazorise services
        Services.AddBlazorise();
        Services.AddBootstrapProviders();
        Services.AddFontAwesomeIcons();
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