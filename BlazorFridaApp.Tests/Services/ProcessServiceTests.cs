using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorFridaApp.Tests.Services;

public class ProcessServiceTests : ServiceTestBase
{
    private readonly ILogger<ProcessServiceTests> _logger;

    public ProcessServiceTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<ProcessServiceTests>();
    }

    [Fact]
    public async Task ShouldListProcesses()
    {
        var processes = await ProcessService.GetProcessesAsync();
        Assert.NotEmpty(processes);

        // Verify at least notepad process is present
        var notepad = processes.FirstOrDefault(p => p.Name.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(notepad);
        Assert.True(notepad.Id > 0);
        Assert.NotEmpty(notepad.Path);
    }

    [Fact]
    public async Task ShouldHandleProcessAttachment()
    {
        // Get notepad process
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        
        // Attach
        var (success, sessionId) = await ProcessService.AttachToProcessAsync(notepad.Id);
        Assert.True(success);
        Assert.NotNull(sessionId);
        Assert.NotEmpty(sessionId);

        // Detach
        await ProcessService.DetachFromProcessAsync(sessionId);

        // Verify we can attach again after detaching
        var (success2, sessionId2) = await ProcessService.AttachToProcessAsync(notepad.Id);
        Assert.True(success2);
        Assert.NotNull(sessionId2);
        Assert.NotEmpty(sessionId2);
    }

    [Fact]
    public async Task ShouldHandleInvalidProcessAttachment()
    {
        // Try to attach to non-existent process
        var (success, sessionId) = await ProcessService.AttachToProcessAsync(-1);
        Assert.False(success);
        Assert.Null(sessionId);
    }

    [Fact]
    public async Task ShouldHandleInvalidProcessDetachment()
    {
        // Should not throw when detaching from invalid session
        await ProcessService.DetachFromProcessAsync("invalid-session-id");
    }

    [Fact]
    public async Task ShouldPreventDuplicateAttachment()
    {
        // Get notepad process
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        
        // First attachment should succeed
        var (success1, sessionId1) = await ProcessService.AttachToProcessAsync(notepad.Id);
        Assert.True(success1);

        try
        {
            // Second attachment should fail
            var (success2, _) = await ProcessService.AttachToProcessAsync(notepad.Id);
            Assert.False(success2);
        }
        finally
        {
            // Clean up
            if (!string.IsNullOrEmpty(sessionId1))
            {
                await ProcessService.DetachFromProcessAsync(sessionId1);
            }
        }
    }
}