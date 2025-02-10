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

public class ProcessSelectorTests : TestContextBase
{
    private readonly Mock<IProcessService> _processServiceMock;

    public ProcessSelectorTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);
        
        JSInterop.SetupVoid("Radzen.preventArrows", _ => true);
        JSInterop.SetupVoid("Radzen.togglePopup", _ => true);
        JSInterop.SetupVoid("Radzen.closePopup", _ => true);
        JSInterop.SetupVoid("Radzen.toggleMenuItem", _ => true);
        JSInterop.SetupVoid("Radzen.destroyPopup", _ => true);
    }

    [Fact]
    public async Task ComponentShouldHandleProcessSelection()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" },
            new() { Id = 2000, Name = "test2.exe" }
        };
        ProcessInfo? selectedProcess = null;

        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, processes)
            .Add(p => p.OnProcessSelected, (ProcessInfo p) => HandleSelection(p))
            .Add(p => p.OnRefreshClick, EventCallback.Factory.Create(this, () => Task.CompletedTask)));

        // Act & Assert initial state
        Assert.Empty(cut.Find(".rz-dropdown").TextContent.Trim());

        // Act - Select process
        await cut.Find(".rz-dropdown").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".rz-dropdown-item")[0].ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal(1000, selectedProcess?.Id);
        Assert.Contains("notepad.exe", cut.Find(".rz-dropdown-text").TextContent);

        // Act - Re-render with updated parameters
        var newProcesses = new List<ProcessInfo>
        {
            new() { Id = 3000, Name = "test3.exe" },
            new() { Id = 4000, Name = "test4.exe" }
        };
        await cut.SetParametersAsync(parameters => parameters
            .Add(p => p.ProcessList, newProcesses));
        cut.Render();

        // Assert - Verify state persists
        Assert.Empty(cut.Find(".rz-dropdown-text").TextContent);

        // Act - Select process
        await cut.Find(".rz-dropdown").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".rz-dropdown-item")[0].ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal(3000, selectedProcess?.Id);
        Assert.Contains("test3.exe", cut.Find(".rz-dropdown-text").TextContent);
    }

    private void HandleSelection(ProcessInfo process)
    {
        selectedProcess = process;
    }
}