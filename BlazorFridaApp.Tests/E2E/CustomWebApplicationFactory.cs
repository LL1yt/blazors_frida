using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Tests.E2E;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly E2ETestConfiguration _configuration;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public CustomWebApplicationFactory(
        E2ETestConfiguration configuration,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _configuration = configuration;
        _configureTestServices = configureTestServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_configuration.TestEnvironment);

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.Test.json", optional: true);
        });

        builder.ConfigureServices(services =>
        {
            // Configure test services
            if (_configuration.UseInMemoryDatabase)
            {
                // Configure in-memory database if needed
            }

            if (_configuration.MockExternalServices)
            {
                // Replace external service clients with mocks
            }

            // Allow test-specific service configuration
            _configureTestServices?.Invoke(services);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
        });
    }
}
