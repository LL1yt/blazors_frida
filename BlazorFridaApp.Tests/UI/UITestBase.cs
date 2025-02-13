using Microsoft.Playwright;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace BlazorFridaApp.Tests.UI;

public abstract class UITestBase : IAsyncLifetime
{
    protected IPlaywright Playwright { get; private set; }
    protected IBrowser Browser { get; private set; }
    protected IPage Page { get; private set; }
    protected IBrowserContext Context { get; private set; }
    protected ILogger Logger { get; }

    protected UITestBase(ILogger logger)
    {
        Logger = logger;
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

        // Create context with tracing enabled
        Context = await Browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true,
            RecordVideoDir = "TestResults/Videos"
        });

        // Start tracing
        await Context.TracingStartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        Page = await Context.NewPageAsync();
        await Page.SetDefaultNavigationTimeoutAsync(10000);
        await Page.SetDefaultTimeoutAsync(5000);
    }

    public async Task DisposeAsync()
    {
        try
        {
            var testName = TestContext.Current?.Test?.TestCase?.TestMethod?.Method?.Name ?? "UnknownTest";
            
            // Stop tracing and save
            await Context.TracingStopAsync(new() 
            { 
                Path = $"TestResults/Traces/{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip" 
            });

            Logger.LogInformation("Saved test trace for {TestName}", testName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save test trace");
        }
        finally
        {
            if (Context != null) await Context.CloseAsync();
            if (Browser != null) await Browser.DisposeAsync();
            Playwright?.Dispose();
        }
    }

    protected async Task NavigateToMemoryScanner()
    {
        await Page.GotoAsync("https://localhost:7235/memory-scanner");
        await Page.WaitForSelectorAsync(".scanner-controls", new() { State = WaitForSelectorState.Visible });
    }

    protected async Task TakeScreenshotAsync([CallerMemberName] string testName = "")
    {
        var screenshotPath = Path.Combine("TestResults", "Screenshots", $"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        Directory.CreateDirectory(Path.Combine("TestResults", "Screenshots"));
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
        Logger.LogInformation("Screenshot saved to {Path}", screenshotPath);
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
}