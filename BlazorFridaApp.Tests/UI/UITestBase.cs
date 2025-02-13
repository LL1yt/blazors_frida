using Microsoft.Playwright;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.IO;

namespace BlazorFridaApp.Tests.UI;

public abstract class UITestBase : IAsyncLifetime
{
    protected IPlaywright Playwright { get; private set; }
    protected IBrowser Browser { get; private set; }
    protected IPage Page { get; private set; }
    protected IBrowserContext Context { get; private set; }
    protected ILogger Logger { get; }

    protected readonly string TestResultsPath = Path.Combine("TestResults", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
    protected readonly string ScreenshotsPath;
    protected readonly string TracesPath;
    protected readonly string VideosPath;

    protected UITestBase(ILogger logger)
    {
        Logger = logger;
        ScreenshotsPath = Path.Combine(TestResultsPath, "Screenshots");
        TracesPath = Path.Combine(TestResultsPath, "Traces");
        VideosPath = Path.Combine(TestResultsPath, "Videos");
        
        Directory.CreateDirectory(ScreenshotsPath);
        Directory.CreateDirectory(TracesPath);
        Directory.CreateDirectory(VideosPath);
    }

    public async Task InitializeAsync()
    {
        Logger.LogInformation("Initializing UI test environment");
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 50
        });

        Context = await Browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true,
            RecordVideoDir = VideosPath
        });

        await Context.TracingStartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
            Title = $"{GetType().Name}_{DateTime.Now:yyyyMMdd_HHmmss}"
        });

        Page = await Context.NewPageAsync();
        await Page.SetDefaultNavigationTimeoutAsync(10000);
        await Page.SetDefaultTimeoutAsync(5000);

        // Add error handling
        Page.Console += (_, e) => 
        {
            if (e.Type == "error")
            {
                Logger.LogError("Browser console error: {Message}", e.Text);
            }
        };

        Page.PageError += (_, e) => 
        {
            Logger.LogError("Page error: {Message}", e.Message);
        };
    }

    protected async Task NavigateToMemoryScanner()
    {
        await Page.GotoAsync("https://localhost:7235/memory-scanner");
        await Page.WaitForSelectorAsync(".scanner-controls", new() { State = WaitForSelectorState.Visible });
    }

    protected async Task TakeScreenshotAsync([CallerMemberName] string? testName = "")
    {
        var screenshotPath = Path.Combine(ScreenshotsPath, $"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await Page.ScreenshotAsync(new() 
        { 
            Path = screenshotPath,
            FullPage = true
        });
        Logger.LogInformation("Screenshot saved to {Path}", screenshotPath);
    }

    protected async Task TakeScreenshotOnFailureAsync(Exception ex, [CallerMemberName] string? testName = "")
    {
        var screenshotPath = Path.Combine(ScreenshotsPath, $"{testName}_FAILED_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await Page.ScreenshotAsync(new() 
        { 
            Path = screenshotPath,
            FullPage = true
        });
        Logger.LogError(ex, "Test failed. Screenshot saved to {Path}", screenshotPath);
    }

    protected async Task WaitForLoadingState(bool expectedState)
    {
        await Page.WaitForFunctionAsync($"state => document.querySelector('.loading-indicator')?.style.display === '{(expectedState ? "block" : "none")}'");
    }

    protected async Task SelectProcess(string processName)
    {
        var processCombobox = Page.GetByRole(AriaRole.Combobox).First;
        await processCombobox.ClickAsync();
        var processOption = Page.GetByText(processName, new() { Exact = false });
        await processOption.ClickAsync();
    }

    protected async Task AssertNoConsoleErrors()
    {
        var errors = await Page.Context.GetConsoleMessagesAsync();
        var errorMessages = errors.Where(m => m.Type == "error").ToList();
        Assert.Empty(errorMessages);
    }

    protected async Task WaitForAjax()
    {
        await Page.WaitForFunctionAsync("() => window.jQuery?.active === 0");
    }

    public async Task DisposeAsync()
    {
        try
        {
            var tracePath = Path.Combine(TracesPath, $"{GetType().Name}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            await Context.TracingStopAsync(new() { Path = tracePath });
            Logger.LogInformation("Trace saved to {Path}", tracePath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save trace");
        }

        await Context?.CloseAsync();
        await Browser?.CloseAsync();
        Playwright?.Dispose();
    }
}