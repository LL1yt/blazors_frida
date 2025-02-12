using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorFridaApp.Tests.Infrastructure;

public class MockTestServiceFactory : TestServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Mock<ITestHealthService> _healthServiceMock;
    private readonly Mock<ITestMemoryService> _memoryServiceMock;
    private readonly Mock<ITestProcessService> _processServiceMock;
    private readonly Mock<ITestScannerService> _scannerServiceMock;
    private readonly Mock<ITestStateService> _stateServiceMock;
    private readonly Mock<ITestFreezeService> _freezeServiceMock;

    public MockTestServiceFactory(TestGrpcConfiguration configuration) : base(configuration)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _healthServiceMock = new Mock<ITestHealthService>();
        _memoryServiceMock = new Mock<ITestMemoryService>();
        _processServiceMock = new Mock<ITestProcessService>();
        _scannerServiceMock = new Mock<ITestScannerService>();
        _stateServiceMock = new Mock<ITestStateService>();
        _freezeServiceMock = new Mock<ITestFreezeService>();

        SetupDefaultMockBehavior();
    }

    private void SetupDefaultMockBehavior()
    {
        // Setup health check mock to return healthy by default
        _healthServiceMock.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryScanner.Proto.Health.HealthCheckResponse
            {
                Status = MemoryScanner.Proto.Health.HealthCheckResponse.Types.ServingStatus.Serving
            });

        // Setup process service mock with basic functionality
        _processServiceMock.Setup(x => x.GetProcessesAsync())
            .ReturnsAsync(new List<MemoryScanner.Models.ProcessInfo>());
        _processServiceMock.Setup(x => x.AttachToProcessAsync(It.IsAny<int>()))
            .ReturnsAsync((true, "test-session"));

        // Setup memory service mock with basic functionality
        _memoryServiceMock.Setup(x => x.ReadMemoryAsync(It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<int>()))
            .ReturnsAsync((new byte[] { 0, 0, 0, 0 }, true, string.Empty));
    }

    public override ITestHealthService CreateHealthService() => _healthServiceMock.Object;
    public override ITestMemoryService CreateMemoryService() => _memoryServiceMock.Object;
    public override ITestProcessService CreateProcessService() => _processServiceMock.Object;
    public override ITestScannerService CreateScannerService() => _scannerServiceMock.Object;
    public override ITestStateService CreateStateService() => _stateServiceMock.Object;
    public override ITestFreezeService CreateFreezeService() => _freezeServiceMock.Object;

    // Expose mocks for test configuration
    public Mock<ITestHealthService> HealthServiceMock => _healthServiceMock;
    public Mock<ITestMemoryService> MemoryServiceMock => _memoryServiceMock;
    public Mock<ITestProcessService> ProcessServiceMock => _processServiceMock;
    public Mock<ITestScannerService> ScannerServiceMock => _scannerServiceMock;
    public Mock<ITestStateService> StateServiceMock => _stateServiceMock;
    public Mock<ITestFreezeService> FreezeServiceMock => _freezeServiceMock;
}