using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Tests.E2E;

[Collection("E2E Tests")]
public class MemoryScannerE2ETests : E2ETestBase
{
    private readonly E2ETestConfiguration _configuration;

    public MemoryScannerE2ETests()
    {
        _configuration = new E2ETestConfiguration
        {
            UseInMemoryDatabase = true,
            MockExternalServices = true
        };
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Configure test-specific services
        // Example: services.AddScoped<IProcessService, MockProcessService>();
    }

    [Fact]
    public async Task GetProcessList_ReturnsSuccessStatusCode()
    {
        // Arrange
        var endpoint = "/api/process/list";

        // Act
        var response = await GetAsync(endpoint);

        // Assert
        await AssertSuccessStatusCode(response);
        var processes = await ReadAsJsonAsync<List<ProcessInfo>>(response);
        Assert.NotNull(processes);
    }

    [Fact]
    public async Task ScanMemory_WithValidInput_ReturnsResults()
    {
        // Arrange
        var endpoint = "/api/memory/scan";
        var request = new MemoryScanRequest  // Renamed to avoid conflict
        {
            ProcessId = 1234,
            Pattern = "test pattern"
        };

        // Act
        var response = await PostAsync(endpoint, request);

        // Assert
        await AssertSuccessStatusCode(response);
        var results = await ReadAsJsonAsync<ScanResults>(response);
        Assert.NotNull(results);
    }
}

// Example DTOs for the tests
public class ProcessInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class MemoryScanRequest  // Renamed from ScanRequest to avoid conflict
{
    public int ProcessId { get; set; }
    public string Pattern { get; set; } = string.Empty;
}

public class ScanResults
{
    public List<string> Matches { get; set; } = new();
}
