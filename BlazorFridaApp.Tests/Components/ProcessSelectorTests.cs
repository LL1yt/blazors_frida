using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Components.Base;
using Moq;
using Xunit;

namespace BlazorFridaApp.Tests.Components;

[TestCategory(TestCategories.Component)]
public class ProcessSelectorTests : ComponentTestBase
{
    private readonly List<ProcessInfo> _defaultProcesses;

    public ProcessSelectorTests()
    {
        _defaultProcesses = new List<ProcessInfo>
        {
            new() { Id = 1000, Name = "notepad.exe" },
            new() { Id = 2000, Name = "test2.exe" }
        };

        var processServiceMock = GetMock<IProcessService>();
        processServiceMock.Setup(x => x.GetProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
        
        processServiceMock.Setup(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultProcesses);
    }

    [Fact]
    [TestCategory(TestCategories.Smoke)]
    public void ShouldRenderSelect()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>();

        // Assert
        AssertComponentRendered(cut);
        Assert.NotNull(cut.Find("select"));
    }

    [Fact]
    [TestCategory(TestCategories.Component)]
    [Retry] // Add retry for potentially flaky async test
    public async Task ShouldLoadProcessesOnInitialization()
    {
        // Arrange & Act
        var cut = RenderComponent<ProcessSelector>();
        
        // Assert
        var options = cut.FindAll("option").ToList();
        Assert.Equal(_defaultProcesses.Count + 1, options.Count); // +1 for default "Select Process" option
        Assert.Contains(options, o => o.TextContent.Contains("notepad.exe"));
        await AssertNoErrorsLogged(cut);
    }

    [Fact]
    [TestCategory(TestCategories.Component)]
    public async Task ShouldRefreshProcessList()
    {
        // Arrange
        var cut = RenderComponent<ProcessSelector>();
        var refreshButton = cut.Find("button[title='Refresh Process List']");
        
        // Act
        await refreshButton.ClickAsync();
        
        // Assert
        GetMock<IProcessService>().Verify(x => x.RefreshProcessesAsync(It.IsAny<CancellationToken>()), Times.Once);
        await AssertNoErrorsLogged(cut);
    }
}