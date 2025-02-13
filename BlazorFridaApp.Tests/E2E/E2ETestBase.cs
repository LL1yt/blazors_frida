using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using BlazorFridaApp.MemoryScanner.Models;
using Xunit;

namespace BlazorFridaApp.Tests.E2E;

public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly ILogger Logger;

    protected E2ETestBase()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override services for testing
                    ConfigureTestServices(services);
                });
            });

        Client = Factory.CreateClient();
        Logger = LoggerFactory
            .Create(builder => builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger(GetType());
    }

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Override this method to configure test-specific services
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    protected async Task<HttpResponseMessage> GetAsync(string endpoint)
    {
        Logger.LogInformation("Making GET request to {Endpoint}", endpoint);
        return await Client.GetAsync(endpoint);
    }

    protected async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T content)
    {
        Logger.LogInformation("Making POST request to {Endpoint}", endpoint);
        return await Client.PostAsJsonAsync(endpoint, content);
    }

    protected async Task<T?> ReadAsJsonAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>();
    }

    protected async Task AssertSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Logger.LogError("Request failed with status {Status}. Response: {Content}", 
                response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }
    }
}
