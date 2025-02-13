using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Diagnostics;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

[Collection(UITestConstants.PerformanceTests)]
public class MemoryScannerPerformanceTests : UITestBase
{
    private readonly Stopwatch _stopwatch = new();

    public MemoryScannerPerformanceTests() : base(
        LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<MemoryScannerPerformanceTests>())
    {
    }

    [RetryTheory(maxRetries: 3, delayMilliseconds: 2000)]
    [InlineData(5)]
    [InlineData(10)]
    [Priority(1)]
    public async Task ShouldMaintainConsistentScanPerformance(int iterations)
    {
        await NavigateToMemoryScanner();
        
        // Load process list and select process
        var processListButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");

        var scanTimes = new List<long>();
        
        for (int i = 0; i < iterations; i++)
        {
            // Enter value and start scan
            await Page.GetByRole(AriaRole.Spinbutton).FillAsync((i * 100).ToString());
            var scanButton = Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" });
            
            _stopwatch.Restart();
            await scanButton.ClickAsync();
            await Page.WaitForSelectorAsync(".results-grid");
            _stopwatch.Stop();
            
            scanTimes.Add(_stopwatch.ElapsedMilliseconds);
            Logger.LogInformation("Scan {Iteration} took {Time}ms", i + 1, scanTimes.Last());
            
            // Wait before next scan
            await Task.Delay(500);
        }

        // Analyze performance
        var avgTime = scanTimes.Average();
        var stdDev = Math.Sqrt(scanTimes.Average(t => Math.Pow(t - avgTime, 2)));
        
        // Assert performance characteristics
        Assert.True(stdDev / avgTime < 0.5, "Performance variance too high"); // Less than 50% variance
        Assert.True(scanTimes.Max() / avgTime < 3.0, "Maximum time too high"); // No scan should take 3x average
    }

    [RetryFact(maxRetries: 3, delayMilliseconds: 1500)]
    [Priority(2)]
    public async Task ShouldHandleRapidUserInteractions()
    {
        await NavigateToMemoryScanner();
        
        // Setup
        var processListButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");

        // Rapid value changes and scans
        _stopwatch.Start();
        for (int i = 0; i < 10; i++)
        {
            var valueInput = Page.GetByRole(AriaRole.Spinbutton);
            await valueInput.FillAsync(i.ToString());
            
            if (i == 0)
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" }).ClickAsync();
            }
            else
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = "Next Memory Scan" }).ClickAsync();
            }
        }
        _stopwatch.Stop();

        // Verify UI responsiveness
        Assert.True(_stopwatch.ElapsedMilliseconds < 30000, "UI interactions too slow"); // Should complete within 30 seconds
        await AssertNoConsoleErrors();
    }

    [RetryFact(maxRetries: 2)]
    [Priority(3)]
    public async Task ShouldHandleLargeResultSets()
    {
        await NavigateToMemoryScanner();
        
        // Setup scan that will return many results
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");
        
        // Set a common value like 0 to get many results
        await Page.GetByRole(AriaRole.Spinbutton).FillAsync("0");
        
        _stopwatch.Start();
        await Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" }).ClickAsync();
        await Page.WaitForSelectorAsync(".results-grid");
        _stopwatch.Stop();

        // Count results
        var resultCount = await Page.EvaluateAsync<int>("() => document.querySelectorAll('.datagrid-row').length");
        Logger.LogInformation("Found {Count} results in {Time}ms", resultCount, _stopwatch.ElapsedMilliseconds);

        // Verify grid performance with large dataset
        Assert.True(_stopwatch.ElapsedMilliseconds < 5000, "Grid rendering too slow");
        await AssertNoConsoleErrors();
    }
}