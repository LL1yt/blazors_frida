using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace BlazorFridaApp.Tests.E2E;

[Collection("E2E Tests")]
public class MemoryScanWorkflowE2ETests : E2ETestBase
{
    public MemoryScanWorkflowE2ETests() : base()
    {
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Configure test services if needed
    }

    [Fact]
    public async Task CompleteMemoryScanWorkflow_ShouldSucceed()
    {
        // Step 1: Get list of processes
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        Assert.NotNull(processes);
        Assert.NotEmpty(processes);

        // Step 2: Select first process
        var targetProcess = processes.First();
        
        // Step 3: Perform initial memory scan
        var scanRequest = new ScanRequest
        {
            ProcessId = targetProcess.Id,
            Pattern = "test_pattern"
        };
        var scanResponse = await PostAsync("/api/memory/scan", scanRequest);
        await AssertSuccessStatusCode(scanResponse);
        var scanResults = await ReadAsJsonAsync<ScanResults>(scanResponse);
        Assert.NotNull(scanResults);

        // Step 4: Get scan state
        var stateResponse = await GetAsync($"/api/memory/state/{targetProcess.Id}");
        await AssertSuccessStatusCode(stateResponse);
        var state = await ReadAsJsonAsync<ScanState>(stateResponse);
        Assert.NotNull(state);

        // Step 5: Verify results are saved
        var savedResultsResponse = await GetAsync($"/api/memory/results/{targetProcess.Id}");
        await AssertSuccessStatusCode(savedResultsResponse);
        var savedResults = await ReadAsJsonAsync<List<MemoryMatch>>(savedResultsResponse);
        Assert.NotNull(savedResults);
    }

    [Fact]
    public async Task MemoryScanWithInvalidProcess_ShouldReturnError()
    {
        var scanRequest = new ScanRequest
        {
            ProcessId = -1, // Invalid process ID
            Pattern = "test_pattern"
        };

        var response = await PostAsync("/api/memory/scan", scanRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentScansOnSameProcess_ShouldBeHandledCorrectly()
    {
        // Get a valid process first
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        // Create multiple scan requests
        var scanTasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 3; i++)
        {
            var scanRequest = new ScanRequest
            {
                ProcessId = targetProcess.Id,
                Pattern = $"pattern_{i}"
            };
            scanTasks.Add(PostAsync("/api/memory/scan", scanRequest));
        }

        // Wait for all scans to complete
        var responses = await Task.WhenAll(scanTasks);

        // Verify that all requests were handled appropriately
        foreach (var response in responses)
        {
            Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict);
        }
    }
}

public class ScanState
{
    public int ProcessId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class MemoryMatch
{
    public long Address { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
