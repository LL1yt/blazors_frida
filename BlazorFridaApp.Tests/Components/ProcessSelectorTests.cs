using Bunit;
using Bunit.TestDoubles;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BlazorFridaApp.Tests.Components;

public class ProcessSelectorTests : BunitContext
{
    private readonly Mock<IProcessService> _processServiceMock;

    public ProcessSelectorTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);
        
        // Setup JS interop for Radzen components
        JSInterop.SetupVoid("Radzen.preventArrows", _ => true);
    }

    [Fact]
    public void ShouldRenderProcessList()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" },
            new() { Id = 2000, Name = "test2.exe" }
        };

        // Act
        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, processes));

        // Assert
        var dropdown = cut.Find(".rz-dropdown");
        Assert.NotNull(dropdown);
        var dropdownList = cut.Find(".rz-dropdown-list");
        Assert.Contains("notepad.exe", dropdownList.TextContent);
        Assert.Contains("test2.exe", dropdownList.TextContent);
    }

    [Fact]
    public async Task ShouldTriggerRefreshProcessList()
    {
        // Arrange
        var onRefreshCalled = false;
        
        // Act
        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, new List<ProcessInfo>())
            .Add(p => p.OnRefreshClick, EventCallback.Factory.Create(this, () => 
            {
                onRefreshCalled = true;
                return Task.CompletedTask;
            })));

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        // Assert
        Assert.True(onRefreshCalled);
    }

    [Fact]
    public async Task ShouldNotifyOnProcessSelected()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" }
        };
        int? selectedProcess = null;
        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, processes)
            .Add(p => p.SelectedProcessId, selectedProcess)
            .Add(p => p.OnProcessSelected, EventCallback.Factory.Create(this, async () => 
            {
                selectedProcess = 1000;
                await Task.CompletedTask;
            })));

        // Act
        var dropdown = cut.Find(".rz-dropdown");
        await dropdown.ClickAsync(new MouseEventArgs());
        var option = cut.Find(".rz-dropdown-item");
        await option.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal(1000, selectedProcess);
    }

    [Fact]
    public void ShouldDisableControlsWhenLoading()
    {
        // Arrange & Act
        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, new List<ProcessInfo>())
            .Add(p => p.IsLoading, true));

        // Assert
        var dropdown = cut.Find(".rz-dropdown");
        var button = cut.Find("button");
        Assert.True(dropdown.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("disabled"));
    }
}