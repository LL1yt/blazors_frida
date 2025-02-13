using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Services;

public class FreezeServiceTests : ServiceTestBase
{
    private readonly ILogger<FreezeServiceTests> _logger;

    public FreezeServiceTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<FreezeServiceTests>();
    }

    [Fact]
    public async Task ShouldHandleFreezeOperations()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Write initial value
        const ulong address = 0x1000;
        var value = BitConverter.GetBytes(12345);
        await MemoryService.WriteMemoryAsync(sessionId, address, value);

        // Freeze value
        var (success, error) = await FreezeService.FreezeValueAsync(
            sessionId,
            address,
            value,
            "int32");
        Assert.True(success);
        Assert.Empty(error);

        // Write different value - should be overwritten by freeze
        await MemoryService.WriteMemoryAsync(
            sessionId,
            address,
            BitConverter.GetBytes(54321));

        // Read back value - should be original frozen value
        var readResult = await MemoryService.ReadMemoryAsync(
            sessionId,
            address,
            value.Length);
        Assert.True(readResult.success);
        Assert.Equal(value, readResult.value);

        // Unfreeze value
        (success, error) = await FreezeService.UnfreezeValueAsync(sessionId, address);
        Assert.True(success);
        Assert.Empty(error);

        // Now write should persist
        var newValue = BitConverter.GetBytes(54321);
        await MemoryService.WriteMemoryAsync(sessionId, address, newValue);
        readResult = await MemoryService.ReadMemoryAsync(sessionId, address, newValue.Length);
        Assert.True(readResult.success);
        Assert.Equal(newValue, readResult.value);
    }

    [Theory]
    [InlineData("int32", 4)]
    [InlineData("float", 4)]
    [InlineData("double", 8)]
    public async Task ShouldValidateValueTypes(string valueType, int size)
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Try to freeze with wrong size value
        var wrongSizeValue = new byte[size + 1];
        var (success, error) = await FreezeService.FreezeValueAsync(
            sessionId,
            0x1000,
            wrongSizeValue,
            valueType);

        Assert.False(success);
        Assert.NotEmpty(error);
        Assert.Contains("size", error.ToLower());
    }

    [Fact]
    public async Task ShouldHandleConcurrentFreezes()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        const int numAddresses = 10;
        var tasks = new List<Task<(bool success, string error)>>();
        var values = new Dictionary<ulong, byte[]>();

        // Start multiple freezes concurrently
        for (ulong i = 0; i < numAddresses; i++)
        {
            var address = 0x1000 + (i * 4);
            var value = BitConverter.GetBytes((int)i);
            values[address] = value;

            tasks.Add(FreezeService.FreezeValueAsync(
                sessionId,
                address,
                value,
                "int32"));
        }

        // Wait for all freezes to complete
        var results = await Task.WhenAll(tasks);

        // All should have succeeded
        Assert.All(results, r => Assert.True(r.success));

        // Verify all values are frozen correctly
        foreach (var kvp in values)
        {
            var readResult = await MemoryService.ReadMemoryAsync(
                sessionId,
                kvp.Key,
                kvp.Value.Length);
            Assert.True(readResult.success);
            Assert.Equal(kvp.Value, readResult.value);
        }
    }

    [Fact]
    public async Task ShouldCleanupOnDetach()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Freeze a value
        const ulong address = 0x1000;
        var value = BitConverter.GetBytes(12345);
        await FreezeService.FreezeValueAsync(
            sessionId,
            address,
            value,
            "int32");

        // Detach from process
        await ProcessService.DetachFromProcessAsync(sessionId);

        // Reattach and verify value is no longer frozen
        var (success, newSessionId) = await ProcessService.AttachToProcessAsync(notepad.Id);
        Assert.True(success);

        var newValue = BitConverter.GetBytes(54321);
        await MemoryService.WriteMemoryAsync(newSessionId, address, newValue);
        var readResult = await MemoryService.ReadMemoryAsync(newSessionId, address, newValue.Length);
        Assert.True(readResult.success);
        Assert.Equal(newValue, readResult.value);
    }
}