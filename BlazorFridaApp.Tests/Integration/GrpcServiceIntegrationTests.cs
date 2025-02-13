using Microsoft.Extensions.Logging;
using Xunit;
using BlazorFridaApp.Tests.Infrastructure;

namespace BlazorFridaApp.Tests.Integration;

[Collection("Grpc Tests")]
public class GrpcServiceIntegrationTests
{
    private readonly GrpcTestFixture _fixture;
    private readonly ILogger<GrpcServiceIntegrationTests> _logger;

    public GrpcServiceIntegrationTests(GrpcTestFixture fixture)
    {
        _fixture = fixture;
        var factory = LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<GrpcServiceIntegrationTests>();
    }

    [Fact]
    public async Task ShouldHandleBasicWorkflow()
    {
        await TestHelper.WithTestContextAsync(async context =>
        {
            // Get list of processes
            var processes = await context.ProcessService.GetProcessesAsync();
            Assert.NotEmpty(processes);

            // Find notepad process
            var notepad = await TestHelper.FindTestProcessAsync(context, "notepad.exe", _logger);
            Assert.NotNull(notepad);

            // Attach to process
            var sessionId = await TestHelper.AttachToProcessAsync(context, notepad.Id, _logger);
            Assert.NotNull(sessionId);

            // Perform memory scan
            var scanResults = await context.ScannerService.ScanMemoryAsync(
                sessionId,
                "int32",
                BitConverter.GetBytes(42),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

            Assert.NotNull(scanResults);
        });
    }

    [Fact]
    public async Task ShouldHandleMemoryOperations()
    {
        await TestHelper.WithTestContextAsync(async context =>
        {
            // Find and attach to notepad
            var notepad = await TestHelper.FindTestProcessAsync(context, "notepad.exe", _logger);
            var sessionId = await TestHelper.AttachToProcessAsync(context, notepad.Id, _logger);

            // Write test value
            var testValue = BitConverter.GetBytes(12345);
            var writeResult = await context.MemoryService.WriteMemoryAsync(
                sessionId,
                0x1000,
                testValue);
            Assert.True(writeResult.success);

            // Read back the value
            var readResult = await context.MemoryService.ReadMemoryAsync(
                sessionId,
                0x1000,
                testValue.Length);
            Assert.True(readResult.success);
            Assert.Equal(testValue, readResult.value);
        });
    }
}