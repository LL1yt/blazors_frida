using Microsoft.Playwright;
using Microsoft.Playwright.Core;
using Xunit;
using System.Threading.Tasks;

namespace BlazorFridaApp.Tests.UI;

[Collection("UI Tests")]
public class MemoryScannerIntegrationTests : IAsyncLifetime
{
    public required IPlaywright _playwright;
    public required IBrowser _browser;
    public required IPage _page;

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
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
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
        
        // First load the process list
        var processListButton = _page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        
        // Wait for scan controls to be fully loaded and interactive
        await _page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        
        // Select a process first to enable scan controls
        var processCombobox = _page.GetByRole(AriaRole.Combobox).First;
        await processCombobox.ClickAsync();
        var notepadOption = _page.GetByText("notepad", new() { Exact = false });
        await notepadOption.ClickAsync();
        
        // Select pattern scan
        var scanTypeCombobox = _page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await scanTypeCombobox.SelectOptionAsync(new[] { "Pattern" });
        
        // Wait for pattern input fields to appear
        await _page.WaitForSelectorAsync("[placeholder='Pattern (hex, space-separated)']");
        
        // Try invalid pattern
        var patternInput = _page.GetByPlaceholder("Pattern (hex, space-separated)");
        var maskInput = _page.GetByPlaceholder("Mask (x - match, ? - wildcard)");
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
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        Assert.False(await scanButton.IsDisabledAsync());
    }

    [Fact]
    public async Task ShouldHandleConnectionErrors()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Wait for the page to be fully loaded
        await _page.WaitForSelectorAsync(".scanner-controls", new() { State = WaitForSelectorState.Visible });
        
        // Wait for process list to be loaded
        await Task.Delay(1000);
        
        // Select first process
        var processSelector = await _page.QuerySelectorAsync("select");
        await processSelector.SelectOptionAsync(new SelectOptionValue[] { new() { Index = 1 } });
        
        // Wait for scan button to be enabled
        var scanButton = await _page.WaitForSelectorAsync("button:has-text('Process Scan')", new() { State = WaitForSelectorState.Visible });
        Assert.NotNull(scanButton);
        
        // Verify error handling
        await scanButton.ClickAsync();
        var errorAlert = await _page.WaitForSelectorAsync(".alert-danger");
        Assert.NotNull(errorAlert);
    }

    [Fact]
    public async Task ShouldSyncStateBetweenComponents()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // First load the process list
        var processListButton = _page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        
        // Wait for process list to be loaded and select notepad
        await _page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        var processCombobox = _page.GetByRole(AriaRole.Combobox).First;
        await processCombobox.ClickAsync();
        var notepadOption = _page.GetByText("notepad", new() { Exact = false });
        await notepadOption.ClickAsync();
        
        // Enter search value and perform first scan
        await _page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        await scanButton.ClickAsync();
        
        // Wait for results and check sync between grid and freezer
        await _page.WaitForSelectorAsync(".results-grid");
        
        // Freeze a value
        var freezeButton = _page.GetByRole(AriaRole.Button, new() { Name = "Freeze" }).First;
        await freezeButton.ClickAsync();
        
        // Verify sync between components
        var frozenIndicator = _page.GetByTestId("frozen-indicator").First;
        Assert.NotNull(await frozenIndicator.ElementHandleAsync());
        
        // Check value handler shows the same value
        var valueDisplay = _page.GetByTestId("current-value").First;
        var displayedValue = await valueDisplay.TextContentAsync();
        Assert.Equal("42", displayedValue);
    }
}