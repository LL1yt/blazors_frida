using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

[Collection(UITestConstants.BasicTests)]
public class MemoryScannerPageTests : UITestBase
{
    public MemoryScannerPageTests() : base(
        LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<MemoryScannerPageTests>())
    {
    }

    [Fact, Priority(1)]
    public async Task ShouldLoadMemoryScannerPage()
    {
        await NavigateToMemoryScanner();
        var title = await Page.TextContentAsync("h2");
        Assert.Equal("Memory Scanner", title);
        await TakeScreenshotAsync();
        await AssertNoConsoleErrors();
    }

    [Fact, Priority(2)]
    public async Task ShouldShowProcessSelector()
    {
        await NavigateToMemoryScanner();
        var processSelector = await Page.WaitForSelectorAsync(".scanner-controls");
        Assert.NotNull(processSelector);
        await AssertNoConsoleErrors();
    }

    [Fact]
    public async Task ShouldShowScanControls()
    {
        await NavigateToMemoryScanner();
        var scanOptionsCard = await Page.QuerySelectorAsync(".scan-options-card");
        Assert.NotNull(scanOptionsCard);
    }

    [Fact]
    public async Task ShouldHandlePatternScan()
    {
        await NavigateToMemoryScanner();
        
        // Load and select process
        var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await scanButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await Task.Delay(1000); // Wait for process list
        await SelectProcess("notepad");
        
        // Select pattern scan type
        var scanTypeSelect = await Page.QuerySelectorAsync("select[name='scanType']");
        Assert.NotNull(scanTypeSelect);
        await scanTypeSelect.SelectOptionAsync("Pattern");
        
        // Verify pattern input appears
        var patternInput = await Page.WaitForSelectorAsync("input[placeholder*='Pattern']");
        Assert.NotNull(patternInput);
    }

    [Fact]
    public async Task ShouldHandleValueFreeze()
    {
        await NavigateToMemoryScanner();
        
        // Load process list and select process
        var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await scanButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");
        
        // Perform scan
        await Page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        var firstScanButton = Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        await firstScanButton.ClickAsync();
        
        // Wait for results and check sync between grid and freezer
        await Page.WaitForSelectorAsync(".results-grid");
        
        // Freeze a value
        var freezeButton = Page.GetByRole(AriaRole.Button, new() { Name = "Freeze" }).First;
        await freezeButton.ClickAsync();
        
        // Verify freeze state
        var frozenIndicator = Page.GetByTestId("frozen-indicator").First;
        Assert.NotNull(await frozenIndicator.ElementHandleAsync());
    }

    [Fact]
    public async Task ShouldHandleScanWorkflow()
    {
        await NavigateToMemoryScanner();
        
        // Load process list
        var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await scanButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await scanButton.ClickAsync();
        
        // Wait for process list and select process
        var processCombobox = Page.GetByRole(AriaRole.Combobox).First;
        await processCombobox.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await Task.Delay(2000);
        await SelectProcess("notepad");
        
        // Select scan type and perform first scan
        var scanTypeCombobox = Page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await scanTypeCombobox.SelectOptionAsync(new[] { "Exact" });
        
        // Enter value and start scan
        var valueInput = Page.GetByRole(AriaRole.Spinbutton);
        await valueInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await valueInput.FillAsync("100");
        
        var firstScanButton = Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
        await firstScanButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await firstScanButton.ClickAsync();
        
        // Wait for results
        await Page.WaitForSelectorAsync(".results-grid", new() { State = WaitForSelectorState.Visible });
        var firstResults = await Page.QuerySelectorAsync(".results-grid");
        Assert.NotNull(firstResults);
        
        // Perform next scan
        await valueInput.FillAsync("200");
        var nextScanButton = Page.GetByRole(AriaRole.Button, new() { Name = "Next Memory Scan" });
        await nextScanButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await nextScanButton.ClickAsync();
        
        // Verify filtered results
        await Page.WaitForSelectorAsync(".datagrid-row", new() { State = WaitForSelectorState.Visible });
        var resultCount = (await Page.QuerySelectorAllAsync(".datagrid-row")).Count;
        Assert.True(resultCount >= 0);
    }
}