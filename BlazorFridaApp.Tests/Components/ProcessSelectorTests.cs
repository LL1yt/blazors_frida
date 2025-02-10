using Bunit;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.Generic;
using BlazorFridaApp.MemoryScanner.Models;
using Blazorise;
using System.Threading;

namespace BlazorFridaApp.Tests.Components;

public class ProcessSelectorTests : TestContextBase
{
    private readonly Mock<IProcessService> _processServiceMock;
    private readonly List<ProcessInfo> _defaultProcesses;

    public ProcessSelectorTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        Services.AddScoped<IProcessService>(_ => _processServiceMock.Object);

        _defaultProcesses = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" },
            new() { Id = 2000, Name = "test2.exe" }
        };

        _processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
        
        _processServiceMock.Setup(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
    }

    [Fact]
    public void ShouldRenderSelect()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>();

        // Assert
        var select = cut.FindComponent<Select<int?>>();
        Assert.NotNull(select);
    }

    [Fact]
    public async Task ShouldLoadProcessesOnInitialization()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>();
        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());
        
        // Wait for re-render
        cut.WaitForState(() => !cut.Instance.IsLoading);
        cut.WaitForState(() => cut.Instance.ProcessList.Count == _defaultProcesses.Count);

        // Assert
        _processServiceMock.Verify(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()), Times.Once);
        var selectItems = cut.FindComponents<SelectItem<int?>>();
        Assert.Equal(3, selectItems.Count); // 2 processes + 1 default item
        Assert.Equal(_defaultProcesses.Count + 1, selectItems.Count); // +1 for the default "Select a process" item
    }

    [Fact]
    public async Task ShouldRefreshProcessList()
    {
        // Arrange
        var cut = RenderComponent<ProcessSelector>();
        await cut.InvokeAsync(() => cut.Instance.InitializeAsync());

        // Act
        var refreshButton = cut.Find("button");
        await refreshButton.ClickAsync(new());

        // Assert
        _processServiceMock.Verify(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}