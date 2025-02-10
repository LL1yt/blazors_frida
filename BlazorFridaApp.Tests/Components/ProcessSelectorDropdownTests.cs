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

public class ProcessSelectorDropdownTests : TestContextBase
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

        var cut = Render<ProcessSelector>(parameters => parameters
            .Add(p => p.ShowRefreshButton, true)
            .Add(p => p.SelectedProcessId, selectedProcessId)
            .Add(p => p.ProcessList, processes));

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
        await cut.InvokeAsync(() => _processServiceMock.Object.RefreshProcessesAsync(CancellationToken.None));
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
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.ShowRefreshButton, true)
            .Add(p => p.SelectedProcessId, selectedProcessId)
            .Add(p => p.ProcessList, processes));

        // Assert 4 - Verify selection survives re-render
        var textAfterRerender = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", textAfterRerender);
        Assert.Equal(1000, selectedProcessId);
    }
    [Fact(DisplayName = "Should display all available processes in dropdown")]
    public void ShouldDisplayAllProcesses()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "process1.exe" },
            new() { Id = 2000, Name = "process2.exe" }
        };

        // Act
        var cut = Render<ProcessSelector>(p => p
            .Add(p => p.ProcessList, processes));

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Equal(2, items.Count);
        Assert.Equal("process1.exe", items[0].TextContent.Trim());
        Assert.Equal("process2.exe", items[1].TextContent.Trim());
    }

    [Fact(DisplayName = "Should refresh process list on refresh button click")]
    public async Task ShouldRefreshProcessList()
    {
        // Arrange
        var initialProcesses = new List<ProcessInfo> { new() { Id = 1000, Name = "old.exe" } };
        var refreshedProcesses = new List<ProcessInfo> { new() { Id = 2000, Name = "new.exe" } };
        
        _processServiceMock.SetupSequence(x => x.GetRunningProcesses())
            .ReturnsAsync(initialProcesses)
            .ReturnsAsync(refreshedProcesses);

        var cut = Render<ProcessSelector>(p => p
            .Add(p => p.ProcessList, initialProcesses)
            .Add(p => p.OnRefreshClick, EventCallback.Factory.Create(this, () => Task.CompletedTask)));

        // Act
        var refreshButton = cut.Find("button.rzi-refresh");
        await refreshButton.ClickAsync(new MouseEventArgs());

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Single(items);
        Assert.Contains("new.exe", items[0].TextContent);
    }

    [Fact(DisplayName = "Should reset selection when process list changes")]
    public void ShouldResetSelectionWhenProcessListChanges()
    {
        // Arrange
        var initialProcesses = new List<ProcessInfo> { new() { Id = 1000, Name = "test.exe" } };
        var cut = Render<ProcessSelector>(p => p
            .Add(p => p.ProcessList, initialProcesses)
            .Add(p => p.SelectedProcessId, 1000));

        // Act - Update process list
        var newProcesses = new List<ProcessInfo> { new() { Id = 3000, Name = "new.exe" } };
        cut.SetParametersAndRender(p => p
            .Add(p => p.ProcessList, newProcesses)
            .Add(p => p.SelectedProcessId, null));

        // Assert
        Assert.Null(cut.Instance.SelectedProcessId);
        var dropdownText = cut.Find(".rz-dropdown-text").TextContent;
        Assert.True(string.IsNullOrWhiteSpace(dropdownText.Trim()));
    }

    [Fact(DisplayName = "Should display error message when process loading fails")]
    public async Task ShouldDisplayErrorMessageOnFailure()
    {
        // Arrange
        _processServiceMock.Setup(x => x.GetRunningProcesses())
            .ThrowsAsync(new Exception("Connection error"));

        // Act
        var cut = Render<ProcessSelector>();
        await cut.InvokeAsync(async () => await cut.Instance.InitializeAsync());

        // Assert
        var errorMessage = cut.Find(".alert-danger");
        Assert.NotNull(errorMessage);
        Assert.Contains("Connection error", errorMessage.TextContent);
    }

    [Fact(DisplayName = "Should filter processes by name")]
    public async Task ShouldFilterProcessesByName()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "chrome.exe" },
            new() { Id = 2000, Name = "notepad.exe" }
        };
        
        var cut = Render<ProcessSelector>(p => p
            .Add(p => p.ProcessList, processes));

        // Act - Enter filter text
        var filterInput = cut.Find("input[type='text']");
        filterInput.Change(new ChangeEventArgs { Value = "note" });

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Single(items);
        Assert.Contains("notepad.exe", items[0].TextContent);
    }

    [Fact(DisplayName = "Should update UI when selected process changes externally")]
    public void ShouldUpdateUIWhenSelectionChanges()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "process1.exe" },
            new() { Id = 2000, Name = "process2.exe" }
        };

        var cut = Render<ProcessSelector>(p => p
            .Add(p => p.ProcessList, processes)
            .Add(p => p.SelectedProcessId, 1000));

        // Act - Change selection externally
        cut.SetParametersAndRender(p => p
            .Add(p => p.SelectedProcessId, 2000));

        // Assert
        var selectedText = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("process2.exe", selectedText);
    }

    private void HandleSelection(int? id)
    {
        // Handle selection logic here
    }
}