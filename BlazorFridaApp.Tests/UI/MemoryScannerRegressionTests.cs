using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BlazorFridaApp.Tests.UI;

[Collection(UITestConstants.RegressionTests)]
public class MemoryScannerRegressionTests : UITestBase
{
    private const string BaselineScreenshotsPath = "TestResults/Baselines";
    private readonly string _diffScreenshotsPath = "TestResults/Diffs";

    public MemoryScannerRegressionTests() : base(
        LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<MemoryScannerRegressionTests>())
    {
        Directory.CreateDirectory(BaselineScreenshotsPath);
        Directory.CreateDirectory(_diffScreenshotsPath);
    }

    [Fact]
    [Priority(1)]
    public async Task ShouldMatchDefaultLayoutSnapshot()
    {
        await NavigateToMemoryScanner();
        await VerifyScreenshot("default_layout");
    }

    [Fact]
    [Priority(2)]
    public async Task ShouldMatchPatternInputLayoutSnapshot()
    {
        await NavigateToMemoryScanner();
        
        // Setup pattern scan view
        var processListButton = Page.GetByRole(AriaRole.Button, new() { Name = "Process Scan" });
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");
        
        var scanTypeCombobox = Page.GetByRole(AriaRole.Combobox).Nth(1);
        await scanTypeCombobox.SelectOptionAsync(new[] { "Pattern" });
        
        await VerifyScreenshot("pattern_input_layout");
    }

    [Fact]
    [Priority(3)]
    public async Task ShouldMatchResultsGridLayoutSnapshot()
    {
        await NavigateToMemoryScanner();
        
        // Setup and perform scan
        await processListButton.ClickAsync();
        await Page.WaitForSelectorAsync("[role='combobox']:not([disabled])");
        await SelectProcess("notepad");
        
        await Page.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        await Page.GetByRole(AriaRole.Button, new() { Name = "First Memory Scan" }).ClickAsync();
        await Page.WaitForSelectorAsync(".results-grid");
        
        await VerifyScreenshot("results_grid_layout");
    }

    private async Task VerifyScreenshot(string name)
    {
        var baselinePath = Path.Combine(BaselineScreenshotsPath, $"{name}.png");
        var actualPath = Path.Combine(_diffScreenshotsPath, $"{name}_actual_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        var diffPath = Path.Combine(_diffScreenshotsPath, $"{name}_diff_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        // Capture current screenshot
        await Page.ScreenshotAsync(new() { Path = actualPath, FullPage = true });

        // If baseline doesn't exist, create it
        if (!File.Exists(baselinePath))
        {
            File.Copy(actualPath, baselinePath);
            return;
        }

        // Compare screenshots
        using var baseline = await Image.LoadAsync<Rgba32>(baselinePath);
        using var actual = await Image.LoadAsync<Rgba32>(actualPath);

        // Basic dimension check
        Assert.Equal(baseline.Width, actual.Width);
        Assert.Equal(baseline.Height, actual.Height);

        // Pixel comparison
        var differences = 0;
        using var diff = new Image<Rgba32>(baseline.Width, baseline.Height);

        for (int y = 0; y < baseline.Height; y++)
        {
            for (int x = 0; x < baseline.Width; x++)
            {
                var baselinePixel = baseline[x, y];
                var actualPixel = actual[x, y];

                if (!PixelsMatch(baselinePixel, actualPixel))
                {
                    differences++;
                    diff[x, y] = new Rgba32(255, 0, 0, 255); // Mark difference in red
                }
                else
                {
                    diff[x, y] = baselinePixel;
                }
            }
        }

        // Save diff image if there are differences
        if (differences > 0)
        {
            await diff.SaveAsync(diffPath);
            Assert.True(false, $"Found {differences} pixel differences. Diff saved to {diffPath}");
        }
    }

    private static bool PixelsMatch(Rgba32 a, Rgba32 b, byte tolerance = 3)
    {
        return Math.Abs(a.R - b.R) <= tolerance &&
               Math.Abs(a.G - b.G) <= tolerance &&
               Math.Abs(a.B - b.B) <= tolerance &&
               Math.Abs(a.A - b.A) <= tolerance;
    }
}