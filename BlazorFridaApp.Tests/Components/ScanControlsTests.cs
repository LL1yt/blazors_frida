using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Blazorise;

namespace BlazorFridaApp.Tests.Components;

public class ScanControlsTests : TestContextBase
{
    public ScanControlsTests()
    {
        // Add Blazorise services
        Services.AddBlazorise();
        Services.AddBootstrapProviders();
        Services.AddFontAwesomeIcons();
    }

    [Fact]
    public void ShouldRenderScanTypeDropdown()
    {
        // Arrange
        var scanTypes = new[] { ScanType.ExactValue, ScanType.Pattern };

        // Act
        var cut = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, scanTypes)
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var dropdown = cut.FindComponent<Select<ScanType>>();
        Assert.NotNull(dropdown);
    }

    [Fact]
    public void ShouldShowPatternInputsForPatternScan()
    {
        // Arrange & Act
        var cut = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.Pattern })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.Pattern)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var memoEdits = cut.FindComponents<MemoEdit>();
        Assert.Equal(2, memoEdits.Count); // One for pattern, one for mask
    }

    [Fact]
    public void ShouldShowNumericInputForExactValue()
    {
        // Arrange & Act
        var cut = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var numericEdit = cut.FindComponent<NumericEdit<int>>();
        Assert.NotNull(numericEdit);
    }

    [Fact]
    public void ShouldShowFirstScanButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.IsFirstScan, true));

        // Assert
        var button = cut.Find("button");
        Assert.Contains("First Memory Scan", button.TextContent);
    }
}