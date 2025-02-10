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
using System.Threading;

namespace BlazorFridaApp.Tests.Components;

public class ProcessSelectorDropdownTests : TestContextBase
{
    private readonly Mock<IProcessService> _processServiceMock;

    public ProcessSelectorDropdownTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);
        
        JSInterop.SetupVoid("Radzen.preventArrows", _ => true);
        JSInterop.SetupVoid("Radzen.togglePopup", _ => true);
        JSInterop.SetupVoid("Radzen.closePopup", _ => true);
        JSInterop.SetupVoid("Radzen.toggleMenuItem", _ => true);
        JSInterop.SetupVoid("Radzen.destroyPopup", _ => true);
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

        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processes);

        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ShowRefreshButton, true)
            .Add(p => p.SelectedProcessId, selectedProcessId));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

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
        Assert.Equal(1000, cut.Instance.SelectedProcessId);
        var dropdownTextAfterSelect = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", dropdownTextAfterSelect);
    }

    [Fact(DisplayName = "Should display all available processes in dropdown")]
    public async Task ShouldDisplayAllProcesses()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "process1.exe" },
            new() { Id = 2000, Name = "process2.exe" }
        };

        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processes);

        // Act
        var cut = RenderComponent<ProcessSelector>();
        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Equal(2, items.Count);
        Assert.Equal("process1.exe (1000)", items[0].TextContent.Trim());
        Assert.Equal("process2.exe (2000)", items[1].TextContent.Trim());
    }

    [Fact(DisplayName = "Should refresh process list on refresh button click")]
    public async Task ShouldRefreshProcessList()
    {
        // Arrange
        var initialProcesses = new List<ProcessInfo> { new() { Id = 1000, Name = "old.exe" } };
        var refreshedProcesses = new List<ProcessInfo> { new() { Id = 2000, Name = "new.exe" } };
        
        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialProcesses);
        _processServiceMock.Setup(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshedProcesses);

        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ShowRefreshButton, true));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Act
        var refreshButton = cut.Find("button");
        await refreshButton.ClickAsync(new MouseEventArgs());

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Single(items);
        Assert.Contains("new.exe", items[0].TextContent);
    }

    [Fact(DisplayName = "Should display error message when process loading fails")]
    public async Task ShouldDisplayErrorMessageOnFailure()
    {
        // Arrange
        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection error"));

        // Act & Assert
        var cut = RenderComponent<ProcessSelector>();
        await Assert.ThrowsAsync<Exception>(() => cut.Instance.InitializeAsync());
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
        
        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processes);

        var cut = RenderComponent<ProcessSelector>();
        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Act - Enter filter text
        var filterInput = cut.Find("input[type='text']");
        filterInput.Change(new ChangeEventArgs { Value = "note" });

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Single(items);
        Assert.Contains("notepad.exe", items[0].TextContent);
    }

    [Fact(DisplayName = "Should update UI when selected process changes externally")]
    public async Task ShouldUpdateUIWhenSelectionChanges()
    {
        // Arrange
        var processes = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "process1.exe" },
            new() { Id = 2000, Name = "process2.exe" }
        };

        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processes);

        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.SelectedProcessId, 1000));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Act - Change selection externally
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.SelectedProcessId, 2000));

        // Assert
        var selectedText = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("process2.exe", selectedText);
    }
}