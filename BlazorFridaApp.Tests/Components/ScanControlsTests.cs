using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Base;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

namespace BlazorFridaApp.Tests.Components;

public class ScanControlsTests : TestContextBase
{
    private readonly Mock<IProcessMemoryScanner> _scannerMock;

    public ScanControlsTests()
    {
        _scannerMock = new Mock<IProcessMemoryScanner>();
        Services.AddSingleton(_scannerMock.Object);
        
        Services
            .AddBlazorise()
            .AddBootstrap5Providers()
            .AddFontAwesomeIcons();
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
        var dropdown = cut.Find(".scan-type-select");
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
        var inputs = cut.FindAll(".pattern-input, .mask-input");
        Assert.Equal(2, inputs.Count); // One for pattern, one for mask
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
        var numericEdit = cut.Find("input[type='number']");
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