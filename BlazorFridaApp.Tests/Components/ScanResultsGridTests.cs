using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;
using System.Collections.Generic;
using Blazorise;
using Blazorise.DataGrid;

namespace BlazorFridaApp.Tests.Components;

public class ScanResultsGridTests : TestContextBase
{
    private readonly IRenderedComponent<ScanResultsGrid> _component;
    private readonly List<IntPtr> _testResults = new() { new IntPtr(0x1000), new IntPtr(0x2000) };

    public ScanResultsGridTests()
    {
        // Render component
        _component = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, _testResults)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, _ => 42)
            .Add(p => p.IsFrozen, _ => false));
    }

    [Fact]
    public void ShouldRenderDataGrid()
    {
        // Assert
        var grid = _component.FindComponent<DataGrid<IntPtr>>();
        Assert.NotNull(grid);
    }

    [Fact]
    public void ShouldDisplayCorrectValueFormat()
    {
        // Arrange
        var component = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, _testResults)
            .Add(p => p.ValueType, MemoryValueType.Float)
            .Add(p => p.GetCurrentValue, _ => 42)
            .Add(p => p.IsFrozen, _ => false));

        // Assert
        var numericEdit = component.FindComponent<NumericEdit<int>>();
        Assert.NotNull(numericEdit);
    }
}