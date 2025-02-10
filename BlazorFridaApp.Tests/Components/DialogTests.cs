using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Models;
using Blazorise;
using Blazorise.DataGrid;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorFridaApp.Tests.Components;

public class DialogTests : TestContextBase
{
    [Fact]
    public async Task SaveConfigDialog_Should_RaiseEventOnSave()
    {
        // Arrange
        string? savedConfig = null;
        var cut = RenderComponent<SaveConfigDialog>(parameters => parameters
            .Add(p => p.OnConfigSaved, EventCallback.Factory.Create<string>(this, name => savedConfig = name)));

        // Act
        var textEdit = cut.FindComponent<TextEdit>();
        await textEdit.InvokeAsync(() => textEdit.Instance.TextChanged.InvokeAsync("TestConfig"));

        var saveButton = cut.Find("button[color='Primary']");
        await saveButton.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal("TestConfig", savedConfig);
    }

    [Fact]
    public void LoadConfigDialog_Should_RaiseEventOnSelect()
    {
        // Arrange
        var configs = new Dictionary<string, ScannerConfig>
        {
            { "Test", new ScannerConfig() }
        };

        ScannerConfig? selectedConfig = null;
        var cut = RenderComponent<LoadConfigDialog>(parameters => parameters
            .Add(p => p.Configs, configs)
            .Add(p => p.OnConfigSelected, EventCallback.Factory.Create<ScannerConfig>(this, config => selectedConfig = config)));

        // Act
        var grid = cut.FindComponent<DataGrid<KeyValuePair<string, ScannerConfig>>>();
        var row = grid.Find("tbody tr");
        row.Click();

        var loadButton = cut.Find("button[color='Primary']");
        loadButton.Click();

        // Assert
        Assert.NotNull(selectedConfig);
    }
}