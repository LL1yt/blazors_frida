using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading;
using AngleSharp.Dom;

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
    public async Task ShouldUpdateUIWhenSelectionChanges()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>(parameters => parameters
            .Add(p => p.Processes, _defaultProcesses)
            .Add(p => p.SelectedProcessId, 1000));

        // Assert initial selection
        var selectedOption = cut.Find("select").GetAttribute("value");
        Assert.Equal("1000", selectedOption);

        // Change selection programmatically through component instance
        var select = cut.Find("select");
        await cut.InvokeAsync(() => select.Change("2000"));
        
        // Assert updated selection
        selectedOption = cut.Find("select").GetAttribute("value");
        Assert.Equal("2000", selectedOption);
    }
}