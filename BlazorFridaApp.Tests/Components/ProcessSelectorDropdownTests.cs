using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;
using System.Collections.Generic;
using Blazorise;
using System.Threading;

namespace BlazorFridaApp.Tests.Components;

public class ProcessSelectorDropdownTests : TestContextBase
{
    private readonly Mock<IProcessService> _processServiceMock;
    private readonly List<ProcessInfo> _defaultProcesses;

    public ProcessSelectorDropdownTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        _defaultProcesses = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" },
            new() { Id = 2000, Name = "test2.exe" }
        };

        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
        
        _processServiceMock.Setup(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
        
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);
    }

    [Fact]
    public async Task ShouldFilterProcessesByName()
    {
        // Arrange
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, _defaultProcesses));
        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Simulate filter input
        await cut.InvokeAsync(() => cut.Instance.HandleFilter(new ChangeEventArgs { Value = "note" }));

        // Assert - After filtering, only one item should be visible
        var filteredText = cut.Markup;
        Assert.Contains("notepad.exe", filteredText);
        Assert.DoesNotContain("test2.exe", filteredText);
    }

    [Fact]
    public async Task ShouldUpdateUIWhenSelectionChanges()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.ProcessList, _defaultProcesses)
            .Add(p => p.SelectedProcessId, 1000));

        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());
        
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.SelectedProcessId, 2000));

        // Assert
        var selectedText = cut.Markup;
        Assert.Contains("test2.exe", selectedText);
    }
}