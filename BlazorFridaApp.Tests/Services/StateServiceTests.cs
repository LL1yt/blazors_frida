using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Services;

public class StateServiceTests : ServiceTestBase
{
    private readonly ILogger<StateServiceTests> _logger;

    public StateServiceTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<StateServiceTests>();
    }

    [Fact]
    public async Task ShouldHandleStateOperations()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Set initial state
        var initialState = new Dictionary<string, byte[]>
        {
            { "test_key", new byte[] { 1, 2, 3, 4 } }
        };
        var initialVersion = "test-version-1";

        // Sync state should succeed
        var syncResult = await StateService.SyncStateAsync(
            sessionId,
            initialState,
            initialVersion);

        Assert.True(syncResult.success);
        Assert.Empty(syncResult.error);
        Assert.NotEqual(initialVersion, syncResult.newVersion);

        // Get state should return our values
        var (state, version) = await StateService.GetStateAsync(sessionId, "test-checkpoint");
        Assert.NotNull(state);
        Assert.NotEmpty(state);
        Assert.Equal(initialState["test_key"], state["test_key"]);
        Assert.NotEmpty(version);
    }

    [Fact]
    public async Task ShouldHandleStateVersioning()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Initial sync
        var initialState = new Dictionary<string, byte[]>
        {
            { "test_key", new byte[] { 1, 2, 3, 4 } }
        };
        var result1 = await StateService.SyncStateAsync(
            sessionId,
            initialState,
            "v1");
        Assert.True(result1.success);
        var version1 = result1.newVersion;

        // Update value
        var updatedState = new Dictionary<string, byte[]>
        {
            { "test_key", new byte[] { 5, 6, 7, 8 } }
        };
        var result2 = await StateService.SyncStateAsync(
            sessionId,
            updatedState,
            version1);
        Assert.True(result2.success);
        var version2 = result2.newVersion;

        // Versions should be different
        Assert.NotEqual("v1", version1);
        Assert.NotEqual(version1, version2);

        // Get latest state
        var (finalState, finalVersion) = await StateService.GetStateAsync(sessionId, "test-checkpoint");
        Assert.Equal(version2, finalVersion);
        Assert.Equal(updatedState["test_key"], finalState["test_key"]);
    }

    [Fact]
    public async Task ShouldHandleStateConflicts()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Try to sync with invalid version
        var result = await StateService.SyncStateAsync(
            sessionId,
            new Dictionary<string, byte[]>(),
            "invalid-version");

        Assert.False(result.success);
        Assert.NotEmpty(result.error);
        Assert.Equal("invalid-version", result.newVersion);
    }

    [Fact]
    public async Task ShouldHandleEmptyState()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        var (state, version) = await StateService.GetStateAsync(sessionId, "non-existent");
        Assert.NotNull(state);
        Assert.Empty(state);
        Assert.NotNull(version);
    }
}