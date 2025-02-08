using Microsoft.Playwright;
using Microsoft.Playwright.Core;
using Xunit;
using System.Threading.Tasks;

namespace BlazorFridaApp.Tests.UI;

[Collection("UI Tests")]
public class MemoryScannerIntegrationTests : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IPage _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task ShouldDisableControlsDuringScanning()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Select process
        var combobox = _page.GetByRole(AriaRole.Combobox);
        await combobox.Nth(0).SelectOptionAsync(new[] { "notepad" });
        
        // Start scan
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Scan" });
        await scanButton.ClickAsync();
        
        // Assert controls are disabled during scan
        var processSelector = _page.GetByRole(AriaRole.Combobox).Nth(0);
        var scanTypeDropdown = _page.GetByRole(AriaRole.Combobox).Nth(1);
        
        Assert.True(await scanButton.IsDisabledAsync());
        Assert.True(await processSelector.IsDisabledAsync());
        Assert.True(await scanTypeDropdown.IsDisabledAsync());
    }

    [Fact]
    public async Task ShouldValidatePatternInput()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Select pattern scan
        var scanTypeCombobox = _page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.SelectOptionAsync(new[] { "Pattern" });
        
        // Try invalid pattern
        var patternInput = _page.GetByPlaceholder("Pattern");
        var maskInput = _page.GetByPlaceholder("Mask");
        await patternInput.FillAsync("invalid pattern");
        await maskInput.FillAsync("xxx");
        
        // Verify error message
        var errorMessage = _page.GetByText("Invalid pattern format");
        var errorElement = await errorMessage.ElementHandleAsync();
        Assert.NotNull(errorElement);
        
        // Try valid pattern
        await patternInput.FillAsync("AA BB CC");
        await maskInput.FillAsync("xxx");
        
        // Verify scan button is enabled
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Scan" });
        Assert.False(await scanButton.IsDisabledAsync());
    }

    [Fact]
    public async Task ShouldHandleConnectionErrors()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Stop the Python server to simulate connection error
        // Note: In real test we would need to properly manage the server process
        
        // Try to perform scan
        var processCombobox = _page.GetByRole(AriaRole.Combobox).Nth(0);
        await processCombobox.SelectOptionAsync(new[] { "notepad" });
        
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Scan" });
        await scanButton.ClickAsync();
        
        // Verify error notification
        var errorNotification = _page.GetByText("Connection error");
        var errorElement = await errorNotification.ElementHandleAsync();
        Assert.NotNull(errorElement);
    }

    [Fact]
    public async Task ShouldSyncStateBetweenComponents()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Select process and perform scan
        var processCombobox = _page.GetByRole(AriaRole.Combobox).Nth(0);
        await processCombobox.SelectOptionAsync(new[] { "notepad" });
        await _page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        await _page.GetByRole(AriaRole.Button, new() { Name = "First Scan" }).ClickAsync();
        
        // Wait for results and check sync between grid and freezer
        await _page.WaitForSelectorAsync(".results-grid");
        
        // Freeze a value
        var freezeButton = _page.GetByRole(AriaRole.Button, new() { Name = "Freeze" }).Nth(0);
        await freezeButton.ClickAsync();
        
        // Verify sync between components
        var frozenIndicator = _page.GetByTestId("frozen-indicator").Nth(0);
        Assert.NotNull(await frozenIndicator.ElementHandleAsync());
        
        // Check value handler shows the same value
        var valueDisplay = _page.GetByTestId("current-value").Nth(0);
        var displayedValue = await valueDisplay.TextContentAsync();
        Assert.Equal("42", displayedValue);
    }
}