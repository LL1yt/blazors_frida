using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Integration;

[Collection("Integration Tests")]
public class ErrorHandlingTests : ServiceTestBase
{
    private readonly ILogger<ErrorHandlingTests> _logger;

    public ErrorHandlingTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<ErrorHandlingTests>();
    }

    [Fact]
    public async Task ShouldHandleProcessTermination()
    {
        // Use a temporary notepad instance that we'll close
        var startInfo = new System.Diagnostics.ProcessStartInfo("notepad.exe");
        using var tempProcess = System.Diagnostics.Process.Start(startInfo);
        Assert.NotNull(tempProcess);

        // Wait for process to be available
        await Task.Delay(1000);

        try
        {
            // Attach to process
            var (success, sessionId) = await ProcessService.AttachToProcessAsync(tempProcess.Id);
            Assert.True(success);
            Assert.NotNull(sessionId);

            // Start a memory scan
            var scanTask = ScannerService.ScanMemoryAsync(
                sessionId,
                "int32",
                BitConverter.GetBytes(12345),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

            // Kill the process while scan is running
            tempProcess.Kill();

            // Scan should fail with appropriate error
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scanTask);
            Assert.Contains("process", ex.Message.ToLower());
        }
        finally
        {
            if (!tempProcess.HasExited)
            {
                tempProcess.Kill();
            }
        }
    }

    [Fact]
    public async Task ShouldHandleParallelOperations()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Prepare multiple operations
        const int numOperations = 10;
        var scanTasks = new List<Task<IEnumerable<ScanResult>>>();
        var writeTasks = new List<Task<(bool success, string error)>>();
        var readTasks = new List<Task<(byte[] value, bool success, string error)>>();

        // Start operations in parallel
        for (int i = 0; i < numOperations; i++)
        {
            var address = 0x1000u + ((ulong)i * 4);
            var value = BitConverter.GetBytes(i);

            scanTasks.Add(ScannerService.ScanMemoryAsync(
                sessionId,
                "int32",
                value,
                "exact",
                new[] { (address, address + 4) }));

            writeTasks.Add(MemoryService.WriteMemoryAsync(
                sessionId,
                address,
                value));

            readTasks.Add(MemoryService.ReadMemoryAsync(
                sessionId,
                address,
                4));
        }

        // Wait for all operations and verify no exceptions
        await Task.WhenAll(
            Task.WhenAll(scanTasks),
            Task.WhenAll(writeTasks),
            Task.WhenAll(readTasks));

        // Verify all operations succeeded
        Assert.All(writeTasks, async t => Assert.True((await t).success));
        Assert.All(readTasks, async t => Assert.True((await t).success));
        Assert.All(scanTasks, async t => Assert.NotNull(await t));
    }

    [Fact]
    public async Task ShouldHandleResourceExhaustion()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Try to scan with excessively large range
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ScannerService.ScanMemoryAsync(
                sessionId,
                "int32",
                BitConverter.GetBytes(12345),
                "exact",
                new[] { ((ulong)0, ulong.MaxValue) }));

        Assert.Contains("range", ex.Message.ToLower());

        // Try to read excessively large region
        ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MemoryService.ReadMemoryAsync(
                sessionId,
                0x1000,
                1024 * 1024 * 1024)); // 1GB read

        Assert.Contains("size", ex.Message.ToLower());
    }

    [Fact]
    public async Task ShouldHandleInvalidSessionIds()
    {
        // Attempt operations with invalid session
        const string invalidSession = "invalid-session-id";

        var tasks = new Task[]
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                ScannerService.ScanMemoryAsync(
                    invalidSession,
                    "int32",
                    BitConverter.GetBytes(12345),
                    "exact",
                    new[] { ((ulong)0, (ulong)0x7FFFFFFF) })),

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                MemoryService.ReadMemoryAsync(
                    invalidSession,
                    0x1000,
                    4)),

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                MemoryService.WriteMemoryAsync(
                    invalidSession,
                    0x1000,
                    new byte[] { 1, 2, 3, 4 }))
        };

        await Task.WhenAll(tasks);
    }
}