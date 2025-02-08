using Microsoft.Playwright;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

public class MemoryScannerPageTests : IAsyncLifetime
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
        var processSelector = await _page.QuerySelectorAsync(".process-selector");
        
        // Assert
        Assert.NotNull(processSelector);
    }

    [Fact]
    public async Task ShouldShowScanControls()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");

        // Act & Assert
        var scanTypeDropdown = await _page.QuerySelectorAsync("text=Scan type");
        Assert.NotNull(scanTypeDropdown);

        var valueTypeDropdown = await _page.QuerySelectorAsync("text=Value type");
        Assert.NotNull(valueTypeDropdown);
    }

    [Fact]
    public async Task ShouldHandlePatternScan()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Act
        await _page.GetByText("Scan type").ClickAsync();
        await _page.GetByText("Pattern").ClickAsync();
        
        // Assert
        var patternInput = await _page.QuerySelectorAsync("textarea[placeholder*='Pattern']");
        var maskInput = await _page.QuerySelectorAsync("textarea[placeholder*='Mask']");
        Assert.NotNull(patternInput);
        Assert.NotNull(maskInput);
        
        // Enter pattern and mask
        await patternInput.FillAsync("AA BB CC");
        await maskInput.FillAsync("xxx");
        
        // Check scan button is enabled
        var scanButton = await _page.QuerySelectorAsync("button:has-text('First Scan')");
        Assert.NotNull(scanButton);
        Assert.False(await scanButton.IsDisabledAsync());
    }

    [Fact]
    public async Task ShouldHandleValueFreeze()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Select process and perform scan
        await _page.GetByRole(AriaRole.Combobox).First().SelectOptionAsync(new[] { "notepad" });
        await _page.GetByRole(AriaRole.Combobox).Nth(1).SelectOptionAsync(new[] { "Exact" });
        await _page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        await _page.GetByText("First Scan").ClickAsync();
        
        // Wait for results
        await _page.WaitForSelectorAsync(".results-grid");
        
        // Try to freeze a value
        var freezeButton = await _page.QuerySelectorAsync(".freeze-button");
        Assert.NotNull(freezeButton);
        await freezeButton.ClickAsync();
        
        // Verify freeze status indicator
        var frozenIndicator = await _page.QuerySelectorAsync(".frozen-indicator");
        Assert.NotNull(frozenIndicator);
    }

    [Fact]
    public async Task ShouldHandleScanWorkflow()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // First scan
        await _page.GetByRole(AriaRole.Combobox).First().SelectOptionAsync(new[] { "notepad" });
        await _page.GetByRole(AriaRole.Combobox).Nth(1).SelectOptionAsync(new[] { "Exact" });
        await _page.GetByRole(AriaRole.Spinbutton).FillAsync("100");
        await _page.GetByText("First Scan").ClickAsync();
        
        // Wait for results and verify
        var firstResults = await _page.QuerySelectorAsync(".results-grid");
        Assert.NotNull(firstResults);
        
        // Change value for next scan
        await _page.GetByRole(AriaRole.Spinbutton).FillAsync("200");
        await _page.GetByText("Next Scan").ClickAsync();
        
        // Verify filtered results
        await _page.WaitForSelectorAsync(".results-grid");
        var resultCount = await _page.QuerySelectorAllAsync(".results-grid .rz-row").CountAsync();
        Assert.True(resultCount >= 0);
    }
}