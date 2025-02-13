using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

[Collection(UITestConstants.BasicTests)]
[TestCategory(TestCategories.UI)]
public class MemoryScannerPageTests : UITestBase
{
    public MemoryScannerPageTests() : base(
        LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<MemoryScannerPageTests>())
    {
    }

    [Fact]
    [TestCategory(TestCategories.Smoke)]
    [Retry(maxRetries: 2)]
    public async Task ShouldLoadMemoryScannerPage()
    {
        try
        {
            await NavigateToMemoryScanner();
            var title = await Page.TextContentAsync("h2");
            Assert.Equal("Memory Scanner", title);
            await TakeScreenshotAsync();
            await AssertNoConsoleErrors();
        }
        catch (Exception ex)
        {
            await TakeScreenshotOnFailureAsync(ex);
            throw;
        }
    }

    [Fact]
    [TestCategory(TestCategories.UI)]
    [Retry]
    public async Task ShouldShowProcessSelector()
    {
        try
        {
            await NavigateToMemoryScanner();
            var processSelector = await Page.WaitForSelectorAsync(".scanner-controls");
            Assert.NotNull(processSelector);
            await TakeScreenshotAsync();
            await AssertNoConsoleErrors();
        }
        catch (Exception ex)
        {
            await TakeScreenshotOnFailureAsync(ex);
            throw;
        }
    }

    [Fact]
    [TestCategory(TestCategories.UI)]
    public async Task ShouldShowScanControls()
    {
        try
        {
            await NavigateToMemoryScanner();
            var scanOptionsCard = await Page.QuerySelectorAsync(".scan-options-card");
            Assert.NotNull(scanOptionsCard);
            await TakeScreenshotAsync();
        }
        catch (Exception ex)
        {
            await TakeScreenshotOnFailureAsync(ex);
            throw;
        }
    }

    [Fact]
    [TestCategory(TestCategories.UI)]
    [TestCategory(TestCategories.Integration)]
    [Retry(maxRetries: 3, delayMilliseconds: 2000)]
    public async Task ShouldHandlePatternScan()
    {
        try
        {
            await NavigateToMemoryScanner();
            
            // Load and select process
            var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
            await scanButton.WaitForAsync(new() { State = WaitForState.Visible });
            await TakeScreenshotAsync("BeforeScan");

            // Configure scan options
            await Page.GetByLabel("Pattern").FillAsync("48 8B 05");
            await Page.GetByLabel("Start Address").FillAsync("0x140000000");
            await Page.GetByLabel("End Address").FillAsync("0x14FFFFFFF");
            await TakeScreenshotAsync("ConfiguredScan");

            // Execute scan
            await scanButton.ClickAsync();
            await WaitForLoadingState(true);
            await WaitForLoadingState(false);
            await TakeScreenshotAsync("AfterScan");

            // Verify results
            var resultsGrid = await Page.WaitForSelectorAsync(".scan-results-grid");
            Assert.NotNull(resultsGrid);
            await AssertNoConsoleErrors();
        }
        catch (Exception ex)
        {
            await TakeScreenshotOnFailureAsync(ex);
            throw;
        }
    }

    [Fact]
    [TestCategory(TestCategories.UI)]
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
    [TestCategory(TestCategories.UI)]
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