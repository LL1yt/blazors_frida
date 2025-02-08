using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Radzen;

namespace BlazorFridaApp.Tests.Components;

public class ScanControlsTests : TestContext
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
        var scanTypes = new[] { ScanType.Exact, ScanType.Pattern };

        // Act
        var cut = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, scanTypes)
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.ScanType, ScanType.Exact)
            .Add(p => p.ValueType, MemoryValueType.Int32));

        // Assert
        var dropdown = cut.Find("select");
        Assert.NotNull(dropdown);
        Assert.Contains("Exact", dropdown.TextContent);
    }

    [Fact]
    public void ShouldShowPatternInputsWhenPatternScanSelected()
    {
        // Arrange
        var scanTypes = new[] { ScanType.Pattern };
        
        // Act
        var cut = RenderComponent<ScanControls>(parameters => parameters
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
        var cut = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.Exact })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
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
        var cutFirstScan = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.Exact })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.IsFirstScan, true));

        // Assert - First Scan
        var firstScanButton = cutFirstScan.Find("button");
        Assert.Contains("First Scan", firstScanButton.TextContent);

        // Arrange & Act - Next Scan
        var cutNextScan = RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanTypes, new[] { ScanType.Exact })
            .Add(p => p.ValueTypes, new[] { MemoryValueType.Int32 })
            .Add(p => p.IsFirstScan, false));

        // Assert - Next Scan
        var nextScanButton = cutNextScan.Find("button");
        Assert.Contains("Next Scan", nextScanButton.TextContent);
    }
}