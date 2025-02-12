using Microsoft.Extensions.Logging;
using Moq;
using BlazorFridaApp.MemoryScanner.Proto.Health;
using Grpc.Core;

namespace BlazorFridaApp.Tests.Infrastructure;

public class GrpcTestHelper
{
    public static Mock<Health.HealthClient> CreateHealthClientMock(ILogger logger)
    {
        var healthClientMock = new Mock<Health.HealthClient>();
        
        healthClientMock
            .Setup(x => x.CheckAsync(
                It.IsAny<HealthCheckRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns((HealthCheckRequest request, Metadata metadata, DateTime? deadline, CancellationToken token) =>
            {
                // Simulate real service behavior
                if (token.IsCancellationRequested)
                {
                    throw new RpcException(new Status(StatusCode.Cancelled, "Call canceled by the client."));
                }

                var response = new HealthCheckResponse
                {
                    Status = HealthCheckResponse.Types.ServingStatus.Serving
                };

                return new AsyncUnaryCall<HealthCheckResponse>(
                    Task.FromResult(response),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });
            });

        return healthClientMock;
    }
} 