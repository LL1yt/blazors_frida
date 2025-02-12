using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace BlazorFridaApp.Tests.Components;

public class ScanControlsValidationTests : TestBase
{
    private required IRenderedComponent<ScanControls> _component;
    private bool _scanClicked;

    public ScanControlsValidationTests()
    {
        _scanClicked = false;
    }

    private void SetupComponent(ScanType scanType = ScanType.ExactValue, 
                              MemoryValueType valueType = MemoryValueType.Int32,
                              string patternHex = "", 
                              string mask = "",
                              int searchValue = 0,
                              bool isFirstScan = true)
    {
        _component = Context.RenderComponent<ScanControls>(parameters => parameters
            .Add(p => p.ScanType, scanType)
            .Add(p => p.ValueType, valueType)
            .Add(p => p.PatternHex, patternHex)
            .Add(p => p.Mask, mask)
            .Add(p => p.SearchValue, searchValue)
            .Add(p => p.IsFirstScan, isFirstScan)
            .Add(p => p.ScanTypes, Enum.GetValues(typeof(ScanType)))
            .Add(p => p.ValueTypes, Enum.GetValues(typeof(MemoryValueType)))
            .Add(p => p.SelectedProcessId, 1234)
            .Add(p => p.OnScanClick, EventCallback.Factory.Create(this, () => _scanClicked = true))
        );
    }

    [Fact]
    public void ShouldShowError_WhenPatternIsEmpty()
    {
        // Arrange
        SetupComponent(scanType: ScanType.Pattern);

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        var error = _component.Find(".alert-danger");
        Assert.Contains("Pattern cannot be empty", error.TextContent);
        Assert.False(_scanClicked);
    }

    [Fact]
    public void ShouldShowError_WhenMaskIsEmpty()
    {
        // Arrange
        SetupComponent(
            scanType: ScanType.Pattern,
            patternHex: "FF 00"
        );

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        var error = _component.Find(".alert-danger");
        Assert.Contains("Mask cannot be empty", error.TextContent);
        Assert.False(_scanClicked);
    }

    [Fact]
    public void ShouldShowError_WhenPatternIsInvalid()
    {
        // Arrange
        SetupComponent(
            scanType: ScanType.Pattern,
            patternHex: "Invalid Pattern",
            mask: "xx"
        );

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        var error = _component.Find(".alert-danger");
        Assert.Contains("Pattern must be space-separated hex bytes", error.TextContent);
        Assert.False(_scanClicked);
    }

    [Fact]
    public void ShouldShowError_WhenMaskLengthDoesNotMatchPattern()
    {
        // Arrange
        SetupComponent(
            scanType: ScanType.Pattern,
            patternHex: "FF 00",
            mask: "x"
        );

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        var error = _component.Find(".alert-danger");
        Assert.Contains("Mask length must match pattern length", error.TextContent);
        Assert.False(_scanClicked);
    }

    [Fact]
    public void ShouldShowError_WhenStringValueIsEmpty()
    {
        // Arrange
        SetupComponent(valueType: MemoryValueType.String);

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        var error = _component.Find(".alert-danger");
        Assert.Contains("Search value cannot be empty", error.TextContent);
        Assert.False(_scanClicked);
    }

    [Fact]
    public void ShouldShowError_WhenNumericValueIsZero()
    {
        // Arrange
        SetupComponent(valueType: MemoryValueType.Int32, searchValue: 0);

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        var error = _component.Find(".alert-danger");
        Assert.Contains("Search value cannot be empty", error.TextContent);
        Assert.False(_scanClicked);
    }

    [Fact]
    public void ShouldAllowScan_WhenAllValidationsPass()
    {
        // Arrange
        SetupComponent(
            scanType: ScanType.Pattern,
            patternHex: "FF 00",
            mask: "xx"
        );

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        Assert.True(_scanClicked);
        Assert.Empty(_component.FindAll(".alert-danger"));
    }

    [Fact]
    public void ShouldAllowScan_WithValidStringValue()
    {
        // Arrange
        SetupComponent(valueType: MemoryValueType.String);
        
        // Act
        var input = _component.Find(".search-input");
        input.Input("test string");
        
        var button = _component.Find("button");
        button.Click();

        // Assert
        Assert.True(_scanClicked);
        Assert.Empty(_component.FindAll(".alert-danger"));
    }

    [Fact]
    public void ShouldAllowScan_WithValidNumericValue()
    {
        // Arrange
        SetupComponent(valueType: MemoryValueType.Int32, searchValue: 123);

        // Act
        var button = _component.Find("button");
        button.Click();

        // Assert
        Assert.True(_scanClicked);
        Assert.Empty(_component.FindAll(".alert-danger"));
    }

    [Fact]
    public void ShouldUpdateStringValue_WhenTextChanged()
    {
        // Arrange
        var stringValue = string.Empty;
        SetupComponent(
            valueType: MemoryValueType.String,
            searchValue: 0
        );

        // Act
        var input = _component.Find(".search-input");
        input.Input("test string");

        // Assert
        Assert.Equal("test string", _component.Instance.StringSearchValue);
    }
}