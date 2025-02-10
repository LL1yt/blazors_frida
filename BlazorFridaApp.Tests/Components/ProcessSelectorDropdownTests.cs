using Bunit;
using Bunit.TestDoubles;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace BlazorFridaApp.Tests.Components;

public class ProcessSelectorDropdownTests : TestContextBase
{
    private readonly Mock<IProcessService> _processServiceMock;
    private readonly Mock<ILogger<ProcessSelector>> _loggerMock;
    private readonly List<ProcessInfo> _defaultProcesses;

    public ProcessSelectorDropdownTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        _loggerMock = new Mock<ILogger<ProcessSelector>>();
        
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);
        Services.AddScoped<ILogger<ProcessSelector>>(_ => _loggerMock.Object);
        
        _defaultProcesses = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" },
            new() { Id = 2000, Name = "test2.exe" }
        };

        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
        
        _processServiceMock.Setup(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
        
        // Setup Radzen JSInterop with required methods
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("_content/Radzen.Blazor/Radzen.Blazor.js");
        
        // Basic dropdown functionality
        JSInterop.Setup<bool>("Radzen.hasChildren").SetResult(false);
        JSInterop.Setup<bool>("Radzen.isVisible").SetResult(true);
        JSInterop.Setup<object>("Radzen.createPopup").SetResult(new object());
        JSInterop.Setup<object>("Radzen.closePopup").SetResult(new object());
        JSInterop.Setup<object>("Radzen.togglePopup").SetResult(new object());
        JSInterop.Setup<object>("Radzen.destroyPopup").SetResult(new object());
        
        // Dropdown-specific functionality
        JSInterop.Setup<object>("Radzen.focusElement").SetResult(new object());
        JSInterop.Setup<object>("Radzen.selectListItem").SetResult(new object());
        JSInterop.Setup<object>("Radzen.selectListItems").SetResult(new object());
        JSInterop.Setup<string[]>("Radzen.getInputValue").SetResult(new[] { "" });
        JSInterop.Setup<object>("Radzen.openDropDown").SetResult(new object());
        JSInterop.Setup<object>("Radzen.closeDropDown").SetResult(new object());
        JSInterop.Setup<bool>("Radzen.isDropDownOpened").SetResult(true);
        JSInterop.Setup<object>("Radzen.raiseEvent").SetResult(new object());
        JSInterop.Setup<object>("Radzen.SetDropDownValue").SetResult(new object());
        JSInterop.Setup<object>("Radzen.SetDropDownFilter").SetResult(new object());
    }

    [Fact(DisplayName = "Dropdown should maintain selected process after closing")]
    public async Task DropdownShouldMaintainSelection()
    {
        // Arrange
        _loggerMock.Object.LogInformation("Starting DropdownShouldMaintainSelection test");
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, _defaultProcesses)
            .Add(p => p.SelectedProcessId, (int?)null));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Act - Open dropdown and select first process
        var dropdown = cut.Find(".rz-dropdown");
        await dropdown.ClickAsync(new MouseEventArgs());
        
        var options = cut.FindAll(".rz-dropdown-item");
        var option = options[0];
        await option.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal(1000, cut.Instance.SelectedProcessId);
        var dropdownText = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("notepad.exe", dropdownText);
    }

    [Fact(DisplayName = "Should display all available processes in dropdown")]
    public async Task ShouldDisplayAllProcesses()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, _defaultProcesses));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Open dropdown
        var dropdown = cut.Find(".rz-dropdown");
        await dropdown.ClickAsync(new MouseEventArgs());

        // Assert
        var items = cut.FindAll(".rz-dropdown-item");
        Assert.Equal(2, items.Count);
        Assert.Contains("notepad.exe (1000)", items[0].TextContent);
        Assert.Contains("test2.exe (2000)", items[1].TextContent);
    }

    [Fact(DisplayName = "Should refresh process list on refresh button click")]
    public async Task ShouldRefreshProcessList()
    {
        // Arrange
        var refreshedProcesses = new List<ProcessInfo> { new() { Id = 3000, Name = "new.exe" } };
        _processServiceMock.Setup(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshedProcesses);

        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ShowRefreshButton, true)
            .Add(p => p.ProcessList, _defaultProcesses));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Act
        var refreshButton = cut.Find("button");
        await refreshButton.ClickAsync(new MouseEventArgs());

        // Open dropdown to verify contents
        var dropdown = cut.Find(".rz-dropdown");
        await dropdown.ClickAsync(new MouseEventArgs());

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
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.AtLeastOnce);
    }

    [Fact(DisplayName = "Should filter processes by name")]
    public async Task ShouldFilterProcessesByName()
    {
        // Arrange
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, _defaultProcesses));
        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Open dropdown
        var dropdown = cut.Find(".rz-dropdown");
        await dropdown.ClickAsync(new MouseEventArgs());

        // Simulate filter input
        JSInterop.SetupVoid("Radzen.SetDropDownFilter", "note");
        await cut.InvokeAsync(() => cut.Instance.HandleFilter(new ChangeEventArgs { Value = "note" }));

        // Assert - After filtering, only one item should be visible
        var filteredText = cut.Markup;
        Assert.Contains("notepad.exe", filteredText);
        Assert.DoesNotContain("test2.exe", filteredText);
    }

    [Fact(DisplayName = "Should update UI when selected process changes externally")]
    public async Task ShouldUpdateUIWhenSelectionChanges()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, _defaultProcesses)
            .Add(p => p.SelectedProcessId, 1000));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());
        
        JSInterop.SetupVoid("Radzen.SetDropDownValue", 2000);
        
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.SelectedProcessId, 2000));

        // Assert
        var selectedText = cut.Find(".rz-dropdown-text").TextContent;
        Assert.Contains("test2.exe", selectedText);
    }

    private void LogJsInterop(string methodName)
    {
        var expectedIdentifier = $"Radzen.{methodName}";
        _loggerMock.Object.LogInformation($"Checking for JS interop call: {expectedIdentifier}");
        
        var invocations = JSInterop.Invocations
            .Select(i => i.Identifier)
            .ToList();
            
        _loggerMock.Object.LogInformation($"Found invocations: {string.Join(", ", invocations)}");
        
        Assert.True(
            JSInterop.Invocations.Any(i => i.Identifier.EndsWith(expectedIdentifier)),
            $"Expected to find JS interop call to {expectedIdentifier}"
        );
    }
}