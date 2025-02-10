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

namespace BlazorFridaApp.Tests.Components;

public class ProcessSelectorDropdownTests : BunitTestContext
{
    private readonly Mock<IProcessService> _processServiceMock;

    public ProcessSelectorDropdownTests()
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

    [Fact(DisplayName = "Dropdown should maintain selected process after closing")]
    public async Task DropdownShouldMaintainSelection()
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
            .Add(p => p.OnProcessSelected, (EventCallback<int?>)EventCallback.Factory.Create<int?>(this, id =>
            {
                selectedProcessId = id;
                return Task.CompletedTask;
            })));

        // Act 1 - Open dropdown and verify initial state
        var dropdown = cut.Find(".rz-dropdown");
        var initialText = dropdown.TextContent;
        Assert.True(string.IsNullOrWhiteSpace(initialText.Trim()), "Dropdown should be empty initially");

        // Act 2 - Click to open dropdown
        await dropdown.ClickAsync(new MouseEventArgs());
        
        // Act 3 - Select first process
        var option = cut.FindAll(".rz-dropdown-item").First();
        await option.ClickAsync(new MouseEventArgs());

        // Assert 1 - Verify selection was made
        Assert.Equal(1000, selectedProcessId);
        var dropdownTextAfterSelect = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", dropdownTextAfterSelect);

        // Act 4 - Close dropdown
        await cut.InvokeAsync(() => JSInterop.InvokeAsync<object>("Radzen.closePopup"));
        await Task.Delay(100); // Give UI time to update

        // Assert 2 - Verify selection persists after closing
        var dropdownTextAfterClose = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", dropdownTextAfterClose);
        Assert.Equal(1000, selectedProcessId);

        // Act 5 - Re-open dropdown
        await dropdown.ClickAsync(new MouseEventArgs());

        // Assert 3 - Verify selection is still visible
        var finalDropdownText = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", finalDropdownText);
        Assert.Equal(1000, selectedProcessId);

        // Act 6 - Force component re-render
        await cut.InvokeAsync(() => cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.ProcessList, processes)
            .Add(p => p.SelectedProcessId, selectedProcessId)));

        // Assert 4 - Verify selection survives re-render
        var textAfterRerender = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", textAfterRerender);
        Assert.Equal(1000, selectedProcessId);
    }
}