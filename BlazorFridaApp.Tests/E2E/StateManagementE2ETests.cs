using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace BlazorFridaApp.Tests.E2E;

[Collection("E2E Tests")]
internal class StateManagementE2ETests : E2ETestBase
{
    public StateManagementE2ETests() : base()
    {
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Configure test-specific services if needed
    }

    [Fact]
    public async Task SyncState_WithValidData_ShouldSucceed()
    {
        // First attach to a process
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        // Create test state data
        var stateData = new MemoryState
        {
            SessionId = attachResult!.SessionId,
            ScanResults = new List<ScanResult>
            {
                new() { Address = 0x1000, Value = 42, Type = "int32" }
            },
            LastScanType = "exact",
            Timestamp = DateTime.UtcNow
        };

        // Sync state
        var syncResponse = await PostAsync("/api/state/sync", stateData);
        await AssertSuccessStatusCode(syncResponse);
    }

    [Fact]
    public async Task GetState_AfterSync_ShouldReturnSavedState()
    {
        // First set up some state
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        var initialState = new MemoryState
        {
            SessionId = attachResult!.SessionId,
            ScanResults = new List<ScanResult>
            {
                new() { Address = 0x1000, Value = 42, Type = "int32" }
            },
            LastScanType = "exact",
            Timestamp = DateTime.UtcNow
        };

        await PostAsync("/api/state/sync", initialState);

        // Then retrieve the state
        var stateResponse = await GetAsync($"/api/state/{attachResult.SessionId}");
        await AssertSuccessStatusCode(stateResponse);
        var savedState = await ReadAsJsonAsync<MemoryState>(stateResponse);

        Assert.NotNull(savedState);
        Assert.Equal(initialState.SessionId, savedState.SessionId);
        Assert.Equal(initialState.LastScanType, savedState.LastScanType);
        Assert.Equal(initialState.ScanResults.Count, savedState.ScanResults.Count);
    }

    [Fact]
    public async Task ClearState_ShouldRemoveAllData()
    {
        // Set up initial state
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        var stateData = new MemoryState
        {
            SessionId = attachResult!.SessionId,
            ScanResults = new List<ScanResult>
            {
                new() { Address = 0x1000, Value = 42, Type = "int32" }
            },
            LastScanType = "exact",
            Timestamp = DateTime.UtcNow
        };

        await PostAsync("/api/state/sync", stateData);

        // Clear the state
        var clearResponse = await PostAsync("/api/state/clear", new { SessionId = attachResult.SessionId });
        await AssertSuccessStatusCode(clearResponse);

        // Verify state is cleared
        var stateResponse = await GetAsync($"/api/state/{attachResult.SessionId}");
        Assert.Equal(HttpStatusCode.NotFound, stateResponse.StatusCode);
    }

    [Fact]
    public async Task StateHistory_ShouldMaintainCorrectOrder()
    {
        // Attach to process
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        // Create multiple state entries
        for (int i = 0; i < 3; i++)
        {
            var state = new MemoryState
            {
                SessionId = attachResult!.SessionId,
                ScanResults = new List<ScanResult>
                {
                    new() { Address = (nuint)(0x1000 + i), Value = 42 + i, Type = "int32" }
                },
                LastScanType = "exact",
                Timestamp = DateTime.UtcNow.AddSeconds(i)
            };

            await PostAsync("/api/state/sync", state);
            await Task.Delay(100); // Ensure different timestamps
        }

        // Get history
        var historyResponse = await GetAsync($"/api/state/{attachResult!.SessionId}/history");
        await AssertSuccessStatusCode(historyResponse);
        var history = await ReadAsJsonAsync<List<MemoryState>>(historyResponse);

        Assert.NotNull(history);
        Assert.Equal(3, history.Count);
        Assert.True(history[0].Timestamp > history[1].Timestamp); // Should be ordered newest first
    }
}

// DTOs
public class MemoryState
{
    public string SessionId { get; set; } = string.Empty;
    public List<ScanResult> ScanResults { get; set; } = new();
    public string LastScanType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ScanResult
{
    public nuint Address { get; set; }
    public object Value { get; set; } = default!;
    public string Type { get; set; } = string.Empty;
}