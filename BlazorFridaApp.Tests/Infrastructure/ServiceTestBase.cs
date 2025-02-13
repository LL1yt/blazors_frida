using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorFridaApp.Tests.Infrastructure;

public abstract class ServiceTestBase : IntegrationTestBase
{
    protected readonly IConfiguration Configuration;
    protected readonly TestServiceFactory ServiceFactory;
    protected readonly ITestHealthService HealthService;
    protected readonly ITestMemoryService MemoryService;
    protected readonly ITestProcessService ProcessService;
    protected readonly ITestScannerService ScannerService;
    protected readonly ITestStateService StateService;
    protected readonly ITestFreezeService FreezeService;

    protected ServiceTestBase() : base()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .Build();
        ServiceFactory = new TestServiceFactory(Configuration);
        HealthService = ServiceFactory.CreateHealthService();
        MemoryService = ServiceFactory.CreateMemoryService();
        ProcessService = ServiceFactory.CreateProcessService();
        ScannerService = ServiceFactory.CreateScannerService();
        StateService = ServiceFactory.CreateStateService();
        FreezeService = ServiceFactory.CreateFreezeService();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var health = await HealthService.CheckHealthAsync();
        if (health.Status != MemoryScanner.Proto.Health.HealthCheckResponse.Types.ServingStatus.Serving)
        {
            throw new InvalidOperationException($"gRPC service is not healthy. Status: {health.Status}");
        }
    }
}