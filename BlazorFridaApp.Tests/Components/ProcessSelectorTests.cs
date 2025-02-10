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

public class ProcessSelectorTests : BunitTestContext
{
    private readonly Mock<IProcessService> _processServiceMock;

    public ProcessSelectorTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);
        
        JSInterop.SetupModule("_content/Radzen.Blazor/Radzen.Blazor.js");
        JSInterop.Setup<object>("Radzen.preventArrows", _ => true);
        JSInterop.Setup<object>("Radzen.togglePopup", _ => true);
        JSInterop.Setup<object>("Radzen.closePopup", _ => true);
        JSInterop.Setup<object>("Radzen.toggleMenuItem", _ => true);
        JSInterop.Setup<object>("Radzen.destroyPopup", _ => true);
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
        int? selectedProcessId = null;

        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, processes)
            .Add(p => p.SelectedProcessId, selectedProcessId)
            .Add(p => p.OnProcessSelected, (EventCallback<int?>) EventCallback.Factory.Create<int?>(this, id =>
            {
                selectedProcessId = id;
                return Task.CompletedTask;
            })));

        // Act & Assert initial state
        Assert.Empty(cut.Find(".rz-dropdown").TextContent.Trim());

        // Act - Select process
        await cut.Find(".rz-dropdown").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".rz-dropdown-item")[0].ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal(1000, selectedProcessId);
        Assert.Contains("notepad.exe", cut.Find(".rz-dropdown-text").TextContent);

        // Act - Re-render with updated parameters
        await cut.InvokeAsync(() => cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.ProcessList, processes)
            .Add(p => p.SelectedProcessId, selectedProcessId)));

        // Assert - Verify state persists
        Assert.contains("notepad.exe", cut.Find(".rz-dropdown-text").TextContent);
    }
}