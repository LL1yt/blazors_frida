using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Radzen;

namespace BlazorFridaApp.Tests.Components;

public class ScanControlsTests : BunitContext
{
    public ScanControlsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<DialogService>();
        Services.AddScoped<NotificationService>();
    }

    [Fact]
    public void ShouldRenderScanTypeDropdown()
    {
        // Arrange
        var scanTypes = new[] { ScanType.ExactValue, ScanType.Pattern };

        // Act
        var cut = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, scanTypes)
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int })
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int));

        // Assert
        var dropdown = cut.Find("select");
        Assert.NotNull(dropdown);
        Assert.Contains("ExactValue", dropdown.TextContent);
    }

    [Fact]
    public void ShouldShowPatternInputsWhenPatternScanSelected()
    {
        // Arrange
        var scanTypes = new[] { ScanType.Pattern };
        
        // Act
        var cut = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, scanTypes)
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int })
            .Add(p => p.ScanType, ScanType.Pattern)
            .Add(p => p.ValueType, MemoryValueType.Int)
            .Add(p => p.PatternHex, "AA BB CC")
            .Add(p => p.Mask, "xxx"));

        // Assert
        var patternInput = cut.Find("textarea[placeholder*='Pattern']");
        var maskInput = cut.Find("textarea[placeholder*='Mask']");
        Assert.NotNull(patternInput);
        Assert.NotNull(maskInput);
    }

    [Fact]
    public void ShouldDisableScanButtonWhenLoading()
    {
        // Arrange & Act
        var cut = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int })
            .Add(p => p.IsLoading, true)
            .Add(p => p.CanScan, true));

        // Assert
        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ShouldShowCorrectButtonTextForFirstAndNextScans()
    {
        // Arrange & Act - First Scan
        var cutFirstScan = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int })
            .Add(p => p.IsFirstScan, true));

        // Assert - First Scan
        var firstScanButton = cutFirstScan.Find("button");
        Assert.Contains("First Scan", firstScanButton.TextContent);

        // Arrange & Act - Next Scan
        var cutNextScan = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int })
            .Add(p => p.IsFirstScan, false));

        // Assert - Next Scan
        var nextScanButton = cutNextScan.Find("button");
        Assert.Contains("Next Scan", nextScanButton.TextContent);
    }
}