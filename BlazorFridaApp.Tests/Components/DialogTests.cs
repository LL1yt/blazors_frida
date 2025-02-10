using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BlazorFridaApp.MemoryScanner.Components;
using BlazorFridaApp.Services;
using Blazorise;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Components;

namespace BlazorFridaApp.Tests.Components;

public class DialogTests : TestContextBase
{
    [Fact]
    public void SaveConfigDialog_Should_RaiseEventOnSave()
    {
        // Arrange
        string savedConfig = null;
        var cut = RenderComponent<SaveConfigDialog>(parameters => parameters
            .Add(p => p.OnConfigSaved, (name) => { savedConfig = name; return Task.CompletedTask; }));

        // Act
        var textEdit = cut.FindComponent<TextEdit>();
        textEdit.SetParametersAndRender(parameters => parameters.Add(p => p.Text, "TestConfig"));

        var saveButton = cut.Find("button[type='button']:nth-child(2)");
        saveButton.Click();

        // Assert
        Assert.Equal("TestConfig", savedConfig);
    }

    [Fact]
    public void LoadConfigDialog_Should_RaiseEventOnSelect()
    {
        // Arrange
        var configs = new Dictionary<string, Models.ScannerConfig>
        {
            { "Test", new Models.ScannerConfig() }
        };

        Models.ScannerConfig selectedConfig = null;
        var cut = RenderComponent<LoadConfigDialog>(parameters => parameters
            .Add(p => p.Configs, configs)
            .Add(p => p.OnConfigSelected, (config) => { selectedConfig = config; return Task.CompletedTask; }));

        // Act
        var grid = cut.FindComponent<DataGrid<KeyValuePair<string, Models.ScannerConfig>>>();
        var row = grid.Find("tbody tr");
        row.Click();

        var loadButton = cut.Find("button[type='button']:nth-child(2)");
        loadButton.Click();

        // Assert
        Assert.NotNull(selectedConfig);
    }
}