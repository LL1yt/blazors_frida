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
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var dropdown = cut.Find(".rz-dropdown");
        Assert.NotNull(dropdown);
    }

    [Fact]
    public void ShouldShowPatternInputsWhenPatternScanSelected()
    {
        // Arrange
        var scanTypes = new[] { ScanType.Pattern };
        
        // Act
        var cut = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, scanTypes)
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.Pattern)
            .Add(p => p.ValueType, MemoryValueType.Int32)
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
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.IsLoading, true)
            .Add(p => p.CanScan, true)
            .Add(p => p.PatternHex, "")
            .Add(p => p.Mask, "")
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var button = cut.Find(".rz-button.rz-button-loading");
        Assert.NotNull(button);
        Assert.Contains("rz-button-loading", button.ClassList);
    }

    [Fact]
    public void ShouldShowCorrectButtonTextForFirstAndNextScans()
    {
        // Arrange & Act - First Scan
        var cutFirstScan = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.IsFirstScan, true)
            .Add(p => p.PatternHex, "")
            .Add(p => p.Mask, "")
            .Add(p => p.CanScan, true)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert - First Scan
        var firstScanButton = cutFirstScan.Find("button.rz-button");
        var firstScanText = firstScanButton.GetAttribute("title") ?? firstScanButton.TextContent;
        Assert.Equal("First Memory Scan", firstScanText.Trim());

        // Arrange & Act - Next Scan
        var cutNextScan = Render<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.ExactValue })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.IsFirstScan, false)
            .Add(p => p.PatternHex, "")
            .Add(p => p.Mask, "")
            .Add(p => p.CanScan, true)
            .Add(p => p.ScanType, ScanType.ExactValue)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert - Next Scan
        var nextScanButton = cutNextScan.Find("button.rz-button");
        var nextScanText = nextScanButton.GetAttribute("title") ?? nextScanButton.TextContent;
        Assert.Equal("Next Memory Scan", nextScanText.Trim());
    }
}