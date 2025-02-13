using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace BlazorFridaApp.Tests.E2E;

[Collection("E2E Tests")]
internal class ErrorHandlingE2ETests : E2ETestBase
{
    public ErrorHandlingE2ETests() : base()
    {
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Configure test-specific services if needed
    }

    [Fact]
    public async Task InvalidProcessOperations_ShouldReturnProperErrors()
    {
        // Test attaching to invalid process
        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, attachResponse.StatusCode);
        var error = await ReadAsJsonAsync<ErrorResponse>(attachResponse);
        Assert.NotNull(error);
        Assert.NotEmpty(error.Message);

        // Test getting info for non-existent process
        var infoResponse = await GetAsync("/api/process/99999999");
        Assert.Equal(HttpStatusCode.NotFound, infoResponse.StatusCode);

        // Test detaching with invalid session
        var detachResponse = await PostAsync("/api/process/detach", new { SessionId = "invalid_session" });
        Assert.Equal(HttpStatusCode.NotFound, detachResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidMemoryOperations_ShouldReturnProperErrors()
    {
        // Test scanning without attaching
        var scanResponse = await PostAsync("/api/memory/scan", new ScanRequest
        {
            ProcessId = 1234,
            Pattern = "test"
        });
        Assert.Equal(HttpStatusCode.BadRequest, scanResponse.StatusCode);

        // Test reading invalid memory address
        var readResponse = await GetAsync("/api/memory/read/invalid_session/0x0");
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        // Test writing to invalid memory address
        var writeResponse = await PostAsync("/api/memory/write", new
        {
            SessionId = "invalid_session",
            Address = "0x0",
            Value = new byte[] { 0x00 }
        });
        Assert.Equal(HttpStatusCode.NotFound, writeResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidStateOperations_ShouldReturnProperErrors()
    {
        // Test getting state for invalid session
        var stateResponse = await GetAsync("/api/state/invalid_session");
        Assert.Equal(HttpStatusCode.NotFound, stateResponse.StatusCode);

        // Test syncing state with invalid data
        var syncResponse = await PostAsync("/api/state/sync", new MemoryState
        {
            SessionId = "invalid_session",
            ScanResults = new List<ScanResult>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, syncResponse.StatusCode);

        // Test clearing non-existent state
        var clearResponse = await PostAsync("/api/state/clear", new { SessionId = "invalid_session" });
        Assert.Equal(HttpStatusCode.NotFound, clearResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldHandleErrors()
    {
        // Get a valid process first
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        // Try to attach to the same process multiple times
        var attachTasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 3; i++)
        {
            attachTasks.Add(PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id }));
        }

        var responses = await Task.WhenAll(attachTasks);
        
        // At least one should succeed, others should fail gracefully
        Assert.Contains(responses, r => r.IsSuccessStatusCode);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TimeoutScenarios_ShouldHandleGracefully()
    {
        // Attach to a process
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        // Test long-running scan operation
        var scanRequest = new ScanRequest
        {
            ProcessId = targetProcess.Id,
            Pattern = "long_pattern",
            Timeout = 1 // 1ms timeout to force timeout
        };

        var scanResponse = await PostAsync("/api/memory/scan", scanRequest);
        Assert.Equal(HttpStatusCode.GatewayTimeout, scanResponse.StatusCode);
    }

    [Fact]
    public async Task ResourceExhaustion_ShouldHandleGracefully()
    {
        // Get a valid process
        var processResponse = await GetAsync("/api/process/list");
        await AssertSuccessStatusCode(processResponse);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(processResponse);
        var targetProcess = processes!.First();

        // Attach to process
        var attachResponse = await PostAsync("/api/process/attach", new { ProcessId = targetProcess.Id });
        await AssertSuccessStatusCode(attachResponse);
        var attachResult = await ReadAsJsonAsync<AttachResult>(attachResponse);

        // Create large state data to test resource limits
        var largeState = new MemoryState
        {
            SessionId = attachResult!.SessionId,
            ScanResults = Enumerable.Range(0, 1000000).Select(i => new ScanResult
            {
                Address = (nuint)i,
                Value = i,
                Type = "int32"
            }).ToList()
        };

        var syncResponse = await PostAsync("/api/state/sync", largeState);
        Assert.Equal(HttpStatusCode.PayloadTooLarge, syncResponse.StatusCode);
    }
}

// DTOs
public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class ScanRequest
{
    public int ProcessId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public int? Timeout { get; set; }
}