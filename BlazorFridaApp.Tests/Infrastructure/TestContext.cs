namespace BlazorFridaApp.Tests.Infrastructure;

public class TestContext : IAsyncDisposable
{
    public string SessionId { get; }
    public TestServiceFactory ServiceFactory { get; }
    public ITestHealthService HealthService { get; }
    public ITestMemoryService MemoryService { get; }
    public ITestProcessService ProcessService { get; }
    public ITestScannerService ScannerService { get; }
    public ITestStateService StateService { get; }
    public ITestFreezeService FreezeService { get; }
    private bool _disposed;

    public TestContext(TestServiceFactory serviceFactory, string sessionId = "test-session")
    {
        SessionId = sessionId;
        ServiceFactory = serviceFactory;
        HealthService = serviceFactory.CreateHealthService();
        MemoryService = serviceFactory.CreateMemoryService();
        ProcessService = serviceFactory.CreateProcessService();
        ScannerService = serviceFactory.CreateScannerService();
        StateService = serviceFactory.CreateStateService();
        FreezeService = serviceFactory.CreateFreezeService();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                // Clean up any active resources
                if (!string.IsNullOrEmpty(SessionId))
                {
                    await ProcessService.DetachFromProcessAsync(SessionId);
                }
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}