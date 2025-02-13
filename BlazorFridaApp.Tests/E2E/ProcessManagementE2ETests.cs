using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace BlazorFridaApp.Tests.E2E;

[Collection("E2E Tests")]
internal class ProcessManagementE2ETests : E2ETestBase
{
    public ProcessManagementE2ETests() : base()
    {
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Configure test-specific services if needed
    }

    [Fact]
    public async Task AttachToProcess_WithValidProcess_ShouldSucceed()
    {
        // Get list of processes first
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        Assert.NotNull(processes);
        Assert.NotEmpty(processes);

        // Try to attach to the first process
        var targetProcess = processes.First();
        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var result = await ReadAsJsonAsync<AttachResult>(attachResponse);
        Assert.NotNull(result);
        Assert.NotNull(result.SessionId);
    }

    [Fact]
    public async Task AttachToProcess_WithInvalidProcess_ShouldFail()
    {
        var response = await PostAsync("/api/process/attach", new { ProcessId = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DetachFromProcess_AfterAttaching_ShouldSucceed()
    {
        // First attach to a process
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        // Then try to detach
        var detachResponse = await PostAsync("/api/process/detach", new { SessionId = attachResult!.SessionId });
        await AssertSuccessStatusCode(detachResponse);
    }

    [Fact]
    public async Task GetProcessInfo_ShouldReturnValidData()
    {
        // Get process list
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        Assert.NotNull(processes);
        Assert.NotEmpty(processes);

        // Get details for first process
        var targetProcess = processes.First();
        var detailsResponse = await GetAsync($"/api/process/{targetProcess.Id}");
        await AssertSuccessStatusCode(detailsResponse);
        var details = await ReadAsJsonAsync<ProcessDetails>(detailsResponse);

        Assert.NotNull(details);
        Assert.Equal(targetProcess.Id, details.Id);
        Assert.NotNull(details.Name);
        Assert.NotNull(details.Path);
    }

    [Fact]
    public async Task MultipleAttachDetach_ShouldHandleCorrectly()
    {
        // Get process list
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        // Perform multiple attach/detach cycles
        for (int i = 0; i < 3; i++)
        {
            // Attach
            var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
            await AssertSuccessStatusCode(attachResponse);
            var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);
            Assert.NotNull(attachResult?.SessionId);

            // Detach
            var detachResponse = await PostAsync("/api/process/detach", new { SessionId = attachResult.SessionId });
            await AssertSuccessStatusCode(detachResponse);
        }
    }
}

// DTOs
public class ProcessDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long MemoryUsage { get; set; }
    public ProcessState State { get; set; }
}

public class AttachResult
{
    public string SessionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public enum ProcessState
{
    Running,
    Suspended,
    Terminated
}