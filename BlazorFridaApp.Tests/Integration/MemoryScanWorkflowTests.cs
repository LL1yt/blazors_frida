using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Integration;

[Collection("Integration Tests")]
public class MemoryScanWorkflowTests : ServiceTestBase
{
    private readonly ILogger<MemoryScanWorkflowTests> _logger;

    public MemoryScanWorkflowTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<MemoryScanWorkflowTests>();
    }

    [Fact]
    public async Task ShouldExecuteFullScanWorkflow()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Write a test value to scan for
        const ulong targetAddress = 0x1000;
        var targetValue = BitConverter.GetBytes(12345);
        var writeResult = await MemoryService.WriteMemoryAsync(
            sessionId,
            targetAddress,
            targetValue);
        Assert.True(writeResult.success);

        // Perform initial scan
        var scanResults = await ScannerService.ScanMemoryAsync(
            sessionId,
            "int32",
            targetValue,
            "exact",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        var results = scanResults.ToList();
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Addresses.Contains(new IntPtr((long)targetAddress)));

        // Modify value and perform next scan
        var newValue = BitConverter.GetBytes(54321);
        await MemoryService.WriteMemoryAsync(
            sessionId,
            targetAddress,
            newValue);

        // Next scan with "changed" comparison
        scanResults = await ScannerService.ScanMemoryAsync(
            sessionId,
            "int32",
            Array.Empty<byte>(), // Not used for "changed" comparison
            "changed",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        results = scanResults.ToList();
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Addresses.Contains(new IntPtr((long)targetAddress)));

        // Freeze the found value
        foreach (var result in results)
        {
            foreach (var address in result.Addresses)
            {
                var (success, error) = await FreezeService.FreezeValueAsync(
                    sessionId,
                    (ulong)address.ToInt64(),
                    newValue,
                    "int32");
                Assert.True(success, error);
            }
        }

        // Try to modify the value - should fail due to freeze
        var attemptValue = BitConverter.GetBytes(99999);
        await MemoryService.WriteMemoryAsync(
            sessionId,
            targetAddress,
            attemptValue);

        // Verify value remains frozen
        var readResult = await MemoryService.ReadMemoryAsync(
            sessionId,
            targetAddress,
            newValue.Length);
        Assert.True(readResult.success);
        Assert.Equal(newValue, readResult.value);

        // Save scan state
        var state = new Dictionary<string, byte[]>
        {
            { "last_scan_addresses", results.SelectMany(r => r.Addresses)
                .SelectMany(a => BitConverter.GetBytes(a.ToInt64())).ToArray() }
        };
        var syncResult = await StateService.SyncStateAsync(sessionId, state, "v1");
        Assert.True(syncResult.success);

        // Clean up
        await ProcessService.DetachFromProcessAsync(sessionId);
    }
}