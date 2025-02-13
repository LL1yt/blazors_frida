using Bunit;
using Microsoft.Extensions.DependencyInjection;
using BlazorFridaApp.Components.Dialogs;
using BlazorFridaApp.Services;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
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
        var input = cut.Find("input.form-control");
        await input.ChangeAsync(new ChangeEventArgs { Value = "TestConfig" });

        var saveButton = cut.Find("button.btn-primary");
        await saveButton.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Equal("TestConfig", savedConfig);
    }

    [Fact]
    public async Task LoadConfigDialog_Should_RaiseEventOnSelect()
    {
        // Arrange
        var configs = new List<ScannerConfig>
        {
            new ScannerConfig { Name = "Test" }
        };

        ScannerConfig? selectedConfig = null;
        var cut = RenderComponent<LoadConfigDialog>(parameters => parameters
            .Add(p => p.Configs, configs)
            .Add(p => p.OnConfigSelected, EventCallback.Factory.Create<ScannerConfig>(this, config => selectedConfig = config)));

        // Act
        var listGroupItem = cut.Find("div.list-group-item");
        await listGroupItem.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.NotNull(selectedConfig);
        Assert.Equal("Test", selectedConfig.Name);
    }
}