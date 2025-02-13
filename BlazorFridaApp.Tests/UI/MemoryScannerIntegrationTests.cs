using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

[Collection(UITestConstants.IntegrationTests)]
public class MemoryScannerIntegrationTests : UITestBase
{
    public MemoryScannerIntegrationTests() : base(
        LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<MemoryScannerIntegrationTests>())
    {
    }

    [RetryFact(maxRetries: 3)]
    [Priority(1)]
    public async Task ShouldDisableControlsDuringScanning()
    {
        await NavigateToMemoryScanner();
        
        // Select process
        var combobox = Page.GetByRole(AriaRole.Combobox);
        await combobox.Nth(0).SelectOptionAsync(new[] { "notepad" });
        
        // Start scan
        var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        await scanButton.ClickAsync();
        
        // Assert controls are disabled during scan
        var processSelector = Page.GetByRole(AriaRole.Combobox).Nth(0);
        var scanTypeDropdown = Page.GetByRole(AriaRole.Combobox).Nth(1);
        
        Assert.True(await scanButton.IsDisabledAsync());
        Assert.True(await processSelector.IsDisabledAsync());
        Assert.True(await scanTypeDropdown.IsDisabledAsync());

        // Wait for scan to complete and verify controls are re-enabled
        await WaitForLoadingState(false);
        Assert.False(await scanButton.IsDisabledAsync());
        Assert.False(await processSelector.IsDisabledAsync());
        Assert.False(await scanTypeDropdown.IsDisabledAsync());

        await AssertNoConsoleErrors();
    }

    [RetryFact(maxRetries: 2)]
    [Priority(2)]
    public async Task ShouldValidatePatternInput()
    {
        await NavigateToMemoryScanner();
        
        // Load process list and select process
        var processListButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");
        
        // Select pattern scan
        var scanTypeCombobox = Page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await scanTypeCombobox.SelectOptionAsync(new[] { "Pattern" });
        
        // Wait for pattern input fields
        await Page.WaitForSelectorAsync("[placeholder='Pattern (hex, space-separated)']");
        
        // Try invalid pattern
        var patternInput = Page.GetByPlaceholder("Pattern (hex, space-separated)");
        var maskInput = Page.GetByPlaceholder("Mask (x - match, ? - wildcard)");
        await patternInput.FillAsync("invalid pattern");
        await maskInput.FillAsync("xxx");
        
        // Verify error message
        var errorMessage = Page.GetByText("Invalid pattern format");
        Assert.NotNull(await errorMessage.ElementHandleAsync());
        
        // Try valid pattern
        await patternInput.FillAsync("AA BB CC");
        await maskInput.FillAsync("xxx");
        
        // Verify scan button is enabled
        var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        Assert.False(await scanButton.IsDisabledAsync());

        await AssertNoConsoleErrors();
    }

    [RetryFact(maxRetries: 3, delayMilliseconds: 2000)]
    [Priority(3)]
    public async Task ShouldSyncStateBetweenComponents()
    {
        await NavigateToMemoryScanner();
        
        // Load process list and select process
        var processListButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");
        
        // Enter search value and perform first scan
        await Page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        await scanButton.ClickAsync();
        
        // Wait for results and verify sync between components
        await Page.WaitForSelectorAsync(".results-grid");
        var freezeButton = Page.GetByRole(AriaRole.Button, new() { Name = "Freeze" }).First;
        await freezeButton.ClickAsync();
        
        // Verify sync between components
        var frozenIndicator = Page.GetByTestId("frozen-indicator").First;
        Assert.NotNull(await frozenIndicator.ElementHandleAsync());
        
        // Check value handler shows the same value
        var valueDisplay = Page.GetByTestId("current-value").First;
        var displayedValue = await valueDisplay.TextContentAsync();
        Assert.Equal("42", displayedValue);

        await AssertNoConsoleErrors();
        await WaitForAjax();
    }

    [RetryFact]
    [Priority(4)]
    public async Task ShouldHandleConnectionErrors()
    {
        await NavigateToMemoryScanner();
        
        // Wait for scan button and click
        var scanButton = await Page.WaitForSelectorAsync("button:has-text('Process Scan')", new() { State = WaitForSelectorState.Visible });
        Assert.NotNull(scanButton);
        
        // Force connection error by disconnecting browser
        await Browser.CloseAsync();
        
        // Should show error alert
        var errorAlert = await Page.WaitForSelectorAsync(".alert-danger");
        Assert.NotNull(errorAlert);
        
        // Take error screenshot
        await TakeScreenshotAsync();
    }
}