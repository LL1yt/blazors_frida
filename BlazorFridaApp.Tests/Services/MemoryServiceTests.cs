using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Services;

public class MemoryServiceTests : ServiceTestBase
{
    private readonly ILogger<MemoryServiceTests> _logger;

    public MemoryServiceTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<MemoryServiceTests>();
    }

    [Fact]
    public async Task ShouldHandleReadWrite()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Write test value
        const ulong address = 0x1000;
        var testValue = BitConverter.GetBytes(12345);
        var writeResult = await MemoryService.WriteMemoryAsync(
            sessionId,
            address,
            testValue);

        Assert.True(writeResult.success);
        Assert.Empty(writeResult.error);

        // Read back the value
        var readResult = await MemoryService.ReadMemoryAsync(
            sessionId,
            address,
            testValue.Length);

        Assert.True(readResult.success);
        Assert.Empty(readResult.error);
        Assert.Equal(testValue, readResult.value);
    }

    [Fact]
    public async Task ShouldHandleInvalidReads()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Try reading from invalid address
        var result = await MemoryService.ReadMemoryAsync(
            sessionId,
            0xFFFFFFFFFFFFFFFF, // Invalid address
            4);

        Assert.False(result.success);
        Assert.NotEmpty(result.error);
        Assert.Empty(result.value);
    }

    [Fact]
    public async Task ShouldHandleInvalidWrites()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Try writing to invalid address
        var result = await MemoryService.WriteMemoryAsync(
            sessionId,
            0xFFFFFFFFFFFFFFFF, // Invalid address
            BitConverter.GetBytes(12345));

        Assert.False(result.success);
        Assert.NotEmpty(result.error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1025)] // Too large
    public async Task ShouldValidateReadSize(int size)
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        await Assert.ThrowsAsync<ArgumentException>(() => 
            MemoryService.ReadMemoryAsync(sessionId, 0x1000, size));
    }
}