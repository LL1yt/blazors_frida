using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace BlazorFridaApp.Tests.Components;

public class ScanResultsGridTests : TestContext
{
    public ScanResultsGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ShouldRenderEmptyGrid()
    {
        // Act
        var cut = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, new List<IntPtr>())
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 0));

        // Assert
        var grid = cut.Find(".rz-datatable");
        Assert.NotNull(grid);
    }

    [Fact]
    public void ShouldRenderResultsWithValues()
    {
        // Arrange
        var results = new List<IntPtr> { new(0x1000), new(0x2000) };
        var values = new Dictionary<IntPtr, int>
        {
            { new(0x1000), 42 },
            { new(0x2000), 100 }
        };

        // Act
        var cut = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => values[addr]));

        // Assert
        var rows = cut.FindAll(".rz-datatable-row");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ShouldHandleValueChange()
    {
        // Arrange
        var results = new List<IntPtr> { new(0x1000) };
        var valueChanged = false;
        var changedAddr = IntPtr.Zero;
        var changedValue = 0;

        var cut = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42)
            .Add(p => p.OnValueChanged, EventCallback.Factory.Create<(IntPtr address, int value)>(this, args =>
            {
                valueChanged = true;
                changedAddr = args.address;
                changedValue = args.value;
                return Task.CompletedTask;
            })));

        // Act
        // Симулируем изменение значения через компонент
        await cut.InvokeAsync(() => cut.Instance.OnValueEdit(new(0x1000), 100));

        // Assert
        Assert.True(valueChanged);
        Assert.Equal(new IntPtr(0x1000), changedAddr);
        Assert.Equal(100, changedValue);
    }

    [Fact]
    public void ShouldRenderFrozenIndicator()
    {
        // Arrange
        var results = new List<IntPtr> { new(0x1000) };
        var frozenAddresses = new HashSet<IntPtr> { new(0x1000) };

        // Act
        var cut = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42)
            .Add(p => p.IsFrozen, (IntPtr addr) => frozenAddresses.Contains(addr)));

        // Assert
        var frozenIndicator = cut.Find(".frozen-indicator");
        Assert.NotNull(frozenIndicator);
    }

    [Fact]
    public void ShouldHandleDifferentValueTypes()
    {
        // Arrange
        var results = new List<IntPtr> { new(0x1000) };

        // Act - Float
        var cutFloat = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Float)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42.5f));

        // Assert - Float
        var floatInput = cutFloat.Find("input[type='number']");
        Assert.NotNull(floatInput);
        Assert.Contains("42.5", floatInput.GetAttribute("value"));

        // Act - Int32
        var cutInt = RenderComponent<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42));

        // Assert - Int32
        var intInput = cutInt.Find("input[type='number']");
        Assert.NotNull(intInput);
        Assert.Contains("42", intInput.GetAttribute("value"));
    }
}