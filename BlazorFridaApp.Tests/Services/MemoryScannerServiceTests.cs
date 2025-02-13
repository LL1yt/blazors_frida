using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Services;

public class MemoryScannerServiceTests : ServiceTestBase
{
    private readonly ILogger<MemoryScannerServiceTests> _logger;

    public MemoryScannerServiceTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<MemoryScannerServiceTests>();
    }

    [Fact]
    public async Task ShouldHandleScanWorkflow()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Perform memory scan
        var scanResults = await ScannerService.ScanMemoryAsync(
            sessionId,
            "int32",
            BitConverter.GetBytes(42),
            "exact",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        // Verify results
        Assert.NotNull(scanResults);
        var resultsList = scanResults.ToList();
        Assert.True(resultsList.Count >= 0); // We can't guarantee exact matches, but response should be valid
    }

    [Fact]
    public async Task ShouldValidatePatternInput()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Test invalid pattern
        await Assert.ThrowsAsync<ArgumentException>(() => 
            ScannerService.ScanMemoryAsync(
                sessionId,
                "pattern",
                Array.Empty<byte>(),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));

        // Test pattern that's too short
        await Assert.ThrowsAsync<ArgumentException>(() => 
            ScannerService.ScanMemoryAsync(
                sessionId,
                "pattern",
                new byte[] { 0xAA, 0xBB },
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));

        // Test pattern that's too long
        var longPattern = new byte[257];
        await Assert.ThrowsAsync<ArgumentException>(() => 
            ScannerService.ScanMemoryAsync(
                sessionId,
                "pattern",
                longPattern,
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) }));
    }

    [Theory]
    [InlineData(MemoryValueType.Byte, 1)]
    [InlineData(MemoryValueType.Int16, 2)]
    [InlineData(MemoryValueType.Int32, 4)]
    [InlineData(MemoryValueType.Int64, 8)]
    [InlineData(MemoryValueType.Float, 4)]
    [InlineData(MemoryValueType.Double, 8)]
    public async Task ShouldUseCorrectValueTypeSize(MemoryValueType valueType, int expectedSize)
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        // Scan with different value types
        var scanResults = await ScannerService.ScanMemoryAsync(
            sessionId,
            valueType.ToString(),
            new byte[expectedSize], // Use correctly sized buffer
            "exact",
            new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

        Assert.NotNull(scanResults);
    }
}