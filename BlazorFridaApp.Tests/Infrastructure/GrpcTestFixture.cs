using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Infrastructure;

public class GrpcTestFixture : IAsyncLifetime
{
    public TestServiceFactory ServiceFactory { get; }
    public TestContext TestContext { get; private set; }
    private readonly ILogger<GrpcTestFixture> _logger;
    private readonly bool _useMocks;

    public GrpcTestFixture(bool useMocks = false)
    {
        _useMocks = useMocks;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<GrpcTestFixture>();
        
        var config = new TestGrpcConfiguration();
        ServiceFactory = _useMocks 
            ? new MockTestServiceFactory(config) 
            : new TestServiceFactory(config);
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing GrpcTestFixture");
        TestContext = await TestHelper.CreateTestContextAsync(_useMocks);
        _logger.LogInformation("GrpcTestFixture initialization completed");
    }

    public async Task DisposeAsync()
    {
        _logger.LogInformation("Disposing GrpcTestFixture");
        if (TestContext != null)
        {
            await TestContext.DisposeAsync();
        }
        _logger.LogInformation("GrpcTestFixture disposal completed");
    }
}

[CollectionDefinition("Grpc Tests")]
public class GrpcTestCollection : ICollectionFixture<GrpcTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}