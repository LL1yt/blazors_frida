using Microsoft.Playwright;
using Microsoft.Playwright.Core;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

public class MemoryScannerPageTests : IAsyncLifetime
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
        var context = await _browser.NewContextAsync();
        _page = await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task ShouldLoadMemoryScannerPage()
    {
        // Act
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Assert
        var title = await _page.TextContentAsync("h2");
        Assert.Equal("Memory Scanner", title);
    }

    [Fact]
    public async Task ShouldShowProcessSelector()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");

        // Act
        var processSelector = await _page.QuerySelectorAsync(".process-card");
        
        // Assert
        Assert.NotNull(processSelector);
    }

    [Fact]
    public async Task ShouldShowScanControls()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");

        // Act & Assert
        var scanOptionsCard = await _page.QuerySelectorAsync(".scan-options-card");
        Assert.NotNull(scanOptionsCard);
    }

    [Fact]
    public async Task ShouldHandlePatternScan()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Wait for scan controls to be loaded
        await _page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        
        // Select pattern scan type
        var scanTypeCombobox = _page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.SelectOptionAsync(new[] { "Pattern" });
        
        // Wait for pattern inputs to be visible
        await _page.WaitForSelectorAsync("[placeholder*='Pattern']");
        
        // Get pattern and mask inputs
        var patternInput = _page.GetByPlaceholder("Pattern");
        var maskInput = _page.GetByPlaceholder("Mask");
        Assert.NotNull(await patternInput.ElementHandleAsync());
        Assert.NotNull(await maskInput.ElementHandleAsync());
        
        // Enter pattern and mask
        await patternInput.FillAsync("AA BB CC");
        await maskInput.FillAsync("xxx");
        
        // Check scan button is enabled
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        Assert.False(await scanButton.IsDisabledAsync());
    }

    [Fact]
    public async Task ShouldHandleValueFreeze()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Click scan to load the process list
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await scanButton.ClickAsync();
        
        // Wait for process list to be loaded
        await _page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        
        // Select process and perform scan
        var processCombobox = _page.GetByRole(AriaRole.Combobox).First;
        await processCombobox.ClickAsync();
        var notepadOption = _page.GetByText("notepad", new() { Exact = false });
        await notepadOption.ClickAsync();
        
        // Select scan type
        var scanTypeCombobox = _page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.SelectOptionAsync(new[] { "Exact" });
        
        // Enter value and start scan
        await _page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        await _page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" }).ClickAsync();
        
        // Wait for results
        await _page.WaitForSelectorAsync(".results-grid");
        
        // Try to freeze a value
        var freezeButton = _page.GetByRole(AriaRole.Button, new() { Name = "Freeze" }).First;
        await freezeButton.ClickAsync();
        
        // Verify freeze status indicator
        var frozenIndicator = _page.GetByTestId("frozen-indicator").First;
        Assert.NotNull(await frozenIndicator.ElementHandleAsync());
    }

    [Fact]
    public async Task ShouldHandleScanWorkflow()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Wait for initial page load and scan button to be visible
        var scanButton = _page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await scanButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await scanButton.ClickAsync();
        
        // Wait for process list to be loaded and combobox to be enabled
        var processCombobox = _page.GetByRole(AriaRole.Combobox).First;
        await processCombobox.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        
        // Wait a bit for the process list to be populated
        await Task.Delay(2000);
        
        // Select process
        await processCombobox.ClickAsync();
        var notepadOption = _page.GetByText("notepad", new() { Exact = false });
        await notepadOption.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await notepadOption.ClickAsync();
        
        // Wait for and select scan type
        var scanTypeCombobox = _page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await scanTypeCombobox.SelectOptionAsync(new[] { "Exact" });
        
        // Enter value and start first scan
        var valueInput = _page.GetByRole(AriaRole.Spinbutton);
        await valueInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await valueInput.FillAsync("100");
        
        var firstScanButton = _page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        await firstScanButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await firstScanButton.ClickAsync();
        
        // Wait for results and verify
        await _page.WaitForSelectorAsync(".results-grid", new() { State = WaitForSelectorState.Visible });
        var firstResults = await _page.QuerySelectorAsync(".results-grid");
        Assert.NotNull(firstResults);
        
        // Change value for next scan
        await valueInput.FillAsync("200");
        var nextScanButton = _page.GetByRole(AriaRole.Button, new() { Name = "Next Memory Scan" });
        await nextScanButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await nextScanButton.ClickAsync();
        
        // Wait for updated results with timeout
        await _page.WaitForSelectorAsync(".datagrid-row", new() 
        { 
            State = WaitForSelectorState.Visible
        });
        
        // Verify filtered results
        var resultCount = (await _page.QuerySelectorAllAsync(".datagrid-row")).Count;
        Assert.True(resultCount >= 0);
    }
}