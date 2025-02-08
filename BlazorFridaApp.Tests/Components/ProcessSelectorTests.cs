using Bunit;
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
    }

    [Fact]
    public void ShouldRenderProcessList()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1234, Name = "test1.exe" },
            new() { Id = 5678, Name = "test2.exe" }
        };

        // Act
        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, processes));

        // Assert
        var select = cut.Find("select");
        Assert.NotNull(select);
        Assert.Contains("test1.exe", select.TextContent);
        Assert.Contains("test2.exe", select.TextContent);
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
        var selectedProcess = 0;
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1234, Name = "test.exe" }
        };

        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, processes)
            .Add(p => p.SelectedProcessId, selectedProcess)
            .Add(p => p.OnProcessSelected, EventCallback.Factory.Create(this, () => 
            {
                selectedProcess = 1234;
                return Task.CompletedTask;
            })));

        // Act
        var select = cut.Find("select");
        await select.ChangeAsync(new ChangeEventArgs { Value = "1234" });

        // Assert
        Assert.Equal(1234, selectedProcess);
    }

    [Fact]
    public void ShouldDisableControlsWhenLoading()
    {
        // Arrange & Act
        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, new List<ProcessInfo>())
            .Add(p => p.IsLoading, true));

        // Assert
        var select = cut.Find("select");
        var button = cut.Find("button");
        Assert.True(select.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("disabled"));
    }
}