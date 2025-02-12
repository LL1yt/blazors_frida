using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using Xunit;
using System.Collections.Generic;
using Blazorise;
using Blazorise.DataGrid;

namespace BlazorFridaApp.Tests.Components;

public class ScanResultsGridTests : TestContextBase
{
    private readonly IRenderedComponent<ScanResultsGrid> _component;
    private readonly List<ScanResult> _testResults;

    public ScanResultsGridTests()
    {
        _testResults = new List<ScanResult>
        {
            new ScanResult 
            { 
                Addresses = new List<nint> { new IntPtr(0x1000) },
                Value = new byte[] { 0x2A },
                ValueType = "Int32",
                ComparisonType = "exact"
            },
            new ScanResult 
            { 
                Addresses = new List<nint> { new IntPtr(0x2000) },
                Value = new byte[] { 0x42 },
                ValueType = "Int32",
                ComparisonType = "exact"
            }
        };

        // Render component
        _component = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, _testResults)
            .Add(p => p.OnFreezeClick, EventCallback.Factory.Create<ScanResult>(this, _ => {})));
    }

    [Fact]
    public void ShouldRenderResults()
    {
        // Assert
        var table = _component.Find("table");
        Assert.NotNull(table);
        var rows = _component.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void ShouldDisplayAddress()
    {
        // Assert
        var firstRow = _component.Find("tbody tr");
        var addressCell = firstRow.Children[0];
        Assert.Contains("1000", addressCell.TextContent);
    }

    [Fact]
    public void ShouldRenderEmptyState()
    {
        // Arrange
        var emptyComponent = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, new List<ScanResult>()));

        // Assert
        var emptyMessage = emptyComponent.Find(".alert-info");
        Assert.Contains("No scan results", emptyMessage.TextContent);
    }

    [Fact]
    public async Task ShouldHandleFreezeClick()
    {
        // Arrange
        var clickedResult = default(ScanResult);
        var component = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, _testResults)
            .Add(p => p.OnFreezeClick, EventCallback.Factory.Create<ScanResult>(this, result => clickedResult = result)));

        // Act
        var freezeButton = component.Find("button");
        await freezeButton.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.NotNull(clickedResult);
        Assert.Equal(_testResults[0], clickedResult);
    }
}