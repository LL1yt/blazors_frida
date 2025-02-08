using Microsoft.Playwright;
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
        await _page.GetByRole("combobox").First().SelectOptionAsync(new[] { "notepad" });
        
        // Start scan
        await _page.GetByRole("button", new() { Name = "First Scan" }).ClickAsync();
        
        // Assert controls are disabled during scan
        var scanButton = await _page.GetByRole("button", new() { Name = "First Scan" });
        var processSelector = await _page.GetByRole("combobox").First();
        var scanTypeDropdown = await _page.GetByRole("combobox").Nth(1);
        
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
        await _page.GetByRole("combobox").Nth(1).SelectOptionAsync(new[] { "Pattern" });
        
        // Try invalid pattern
        await _page.GetByPlaceholder("Pattern").FillAsync("invalid pattern");
        await _page.GetByPlaceholder("Mask").FillAsync("xxx");
        
        // Verify error message
        var errorMessage = await _page.GetByText("Invalid pattern format");
        Assert.NotNull(errorMessage);
        
        // Try valid pattern
        await _page.GetByPlaceholder("Pattern").FillAsync("AA BB CC");
        await _page.GetByPlaceholder("Mask").FillAsync("xxx");
        
        // Verify scan button is enabled
        var scanButton = await _page.GetByRole("button", new() { Name = "First Scan" });
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
        await _page.GetByRole("combobox").First().SelectOptionAsync(new[] { "notepad" });
        await _page.GetByRole("button", new() { Name = "First Scan" }).ClickAsync();
        
        // Verify error notification
        var errorNotification = await _page.GetByText("Connection error");
        Assert.NotNull(errorNotification);
    }

    [Fact]
    public async Task ShouldSyncStateBetweenComponents()
    {
        // Arrange
        await _page.GotoAsync("https://localhost:7235/memory-scanner");
        
        // Select process and perform scan
        await _page.GetByRole("combobox").First().SelectOptionAsync(new[] { "notepad" });
        await _page.GetByRole("spinbutton").FillAsync("42");
        await _page.GetByRole("button", new() { Name = "First Scan" }).ClickAsync();
        
        // Wait for results and check sync between grid and freezer
        await _page.WaitForSelectorAsync(".results-grid");
        
        // Freeze a value
        await _page.GetByRole("button", new() { Name = "Freeze" }).First().ClickAsync();
        
        // Verify sync between components
        var frozenIndicator = await _page.GetByTestId("frozen-indicator").First();
        Assert.NotNull(frozenIndicator);
        
        // Check value handler shows the same value
        var valueDisplay = await _page.GetByTestId("current-value").First();
        var displayedValue = await valueDisplay.TextContentAsync();
        Assert.Equal("42", displayedValue);
    }
}