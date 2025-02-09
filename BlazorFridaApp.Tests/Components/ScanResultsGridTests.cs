using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;
using System.Collections.Generic;
using Radzen;

namespace BlazorFridaApp.Tests.Components;

public class ScanResultsGridTests : TestContextWrapper
{
    public ScanResultsGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
    }

    [Fact]
    public void ShouldRenderEmptyGrid()
    {
        // Act
        var cut = Render<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, new List<IntPtr>())
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 0)
            .Add(p => p.IsFrozen, (IntPtr addr) => false));

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
        var cut = Render<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => values[addr])
            .Add(p => p.IsFrozen, (IntPtr addr) => false));

        // Assert
        var rows = cut.FindAll(".rz-grid-table tr");
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

        var cut = Render<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42)
            .Add(p => p.IsFrozen, (IntPtr addr) => false)
            .Add(p => p.OnValueChanged, EventCallback.Factory.Create<(IntPtr address, int value)>(this, args =>
            {
                valueChanged = true;
                changedAddr = args.address;
                changedValue = args.value;
                return Task.CompletedTask;
            })));

        // Act
        var numericInput = cut.Find(".rz-numeric");
        var changeEvent = new ChangeEventArgs { Value = "100" };
        await cut.InvokeAsync(() => numericInput.Change(changeEvent));

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
        var cut = Render<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42)
            .Add(p => p.IsFrozen, (IntPtr addr) => frozenAddresses.Contains(addr)));

        // Assert
        var frozenButton = cut.Find(".rz-button[disabled]");
        Assert.NotNull(frozenButton);
    }

    [Fact]
    public void ShouldHandleDifferentValueTypes()
    {
        // Arrange
        var results = new List<IntPtr> { new(0x1000) };

        // Act - Float
        var cutFloat = Render<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Float)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => BitConverter.ToInt32(BitConverter.GetBytes(42.5f), 0))
            .Add(p => p.IsFrozen, (IntPtr addr) => false));

        // Assert - Float
        var floatValue = cutFloat.Find("rz-numeric");
        Assert.NotNull(floatValue);
        Assert.Contains("42.50", floatValue.GetAttribute("value"));

        // Act - Int
        var cutInt = Render<ScanResultsGrid>(parameters => parameters
            .Add(p => p.Results, results)
            .Add(p => p.ValueType, MemoryValueType.Int32)
            .Add(p => p.GetCurrentValue, (IntPtr addr) => 42)
            .Add(p => p.IsFrozen, (IntPtr addr) => false));

        // Assert - Int
        var intValue = cutInt.Find("rz-numeric");
        Assert.NotNull(intValue);
        Assert.Contains("42", intValue.GetAttribute("value"));
    }
}