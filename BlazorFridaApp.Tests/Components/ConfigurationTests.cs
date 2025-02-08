using Bunit;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace BlazorFridaApp.Tests.Components;

public class ConfigurationTests : TestContext
{
    private readonly Mock<IScanProfileService> _profileServiceMock;
    private readonly AppDbContext _dbContext;
    private readonly ServiceProvider _serviceProvider;

    public ConfigurationTests()
    {
        _profileServiceMock = new Mock<IScanProfileService>();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("TestMemoryScanner"));
            
        services.AddScoped<IScanProfileService>(_ => _profileServiceMock.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        
        Services.AddScoped<IScanProfileService>(_ => _profileServiceMock.Object);
    }

    [Fact]
    public async Task ShouldSaveAndLoadScanConfiguration()
    {
        // Arrange
        var config = new ScanProfile
        {
            Name = "Test Config",
            ProcessName = "notepad.exe",
            Pattern = new byte[] { 0xAA, 0xBB, 0xCC },
            Mask = "xxx",
            ComparisonType = "exact"
        };

        _profileServiceMock.Setup(x => x.SaveProfileAsync(It.IsAny<ScanProfile>()))
            .ReturnsAsync(config);
        _profileServiceMock.Setup(x => x.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(config);

        // Act
        await _dbContext.ScanProfiles.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        var loadedConfig = await _dbContext.ScanProfiles
            .FirstOrDefaultAsync(p => p.Name == "Test Config");

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(config.ProcessName, loadedConfig.ProcessName);
        Assert.Equal(config.Pattern, loadedConfig.Pattern);
        Assert.Equal(config.Mask, loadedConfig.Mask);
    }

    [Fact]
    public void ShouldHandleConfigurationValidation()
    {
        // Arrange
        var invalidConfig = new ScanProfile
        {
            Name = "", // Invalid - empty name
            ProcessName = "notepad.exe",
            Pattern = new byte[300], // Invalid - too long
            Mask = new string('x', 300) // Invalid - too long
        };

        _profileServiceMock.Setup(x => x.ValidateProfile(It.IsAny<ScanProfile>()))
            .Returns((ScanProfile profile) =>
            {
                var errors = new List<string>();
                if (string.IsNullOrEmpty(profile.Name))
                    errors.Add("Name is required");
                if (profile.Pattern.Length > 256)
                    errors.Add("Pattern is too long");
                if (profile.Mask.Length > 256)
                    errors.Add("Mask is too long");
                return errors;
            });

        // Act
        var validationErrors = _profileServiceMock.Object.ValidateProfile(invalidConfig);

        // Assert
        Assert.NotEmpty(validationErrors);
        Assert.Contains(validationErrors, e => e.Contains("Name is required"));
        Assert.Contains(validationErrors, e => e.Contains("Pattern is too long"));
        Assert.Contains(validationErrors, e => e.Contains("Mask is too long"));
    }

    [Fact]
    public async Task ShouldHandleDuplicateConfigurationNames()
    {
        // Arrange
        var config1 = new ScanProfile
        {
            Name = "Test Config",
            ProcessName = "notepad.exe"
        };

        var config2 = new ScanProfile
        {
            Name = "Test Config", // Same name
            ProcessName = "calc.exe"
        };

        // Act
        await _dbContext.ScanProfiles.AddAsync(config1);
        await _dbContext.SaveChangesAsync();

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await _dbContext.ScanProfiles.AddAsync(config2);
            await _dbContext.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task ShouldTrackConfigurationUsage()
    {
        // Arrange
        var config = new ScanProfile
        {
            Name = "Test Config",
            ProcessName = "notepad.exe",
            Created = DateTime.UtcNow.AddDays(-1),
            LastUsed = DateTime.UtcNow.AddDays(-1)
        };

        await _dbContext.ScanProfiles.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Act
        config.LastUsed = DateTime.UtcNow;
        _dbContext.ScanProfiles.Update(config);
        await _dbContext.SaveChangesAsync();

        // Assert
        var loadedConfig = await _dbContext.ScanProfiles
            .FirstOrDefaultAsync(p => p.Name == "Test Config");
        Assert.NotNull(loadedConfig);
        Assert.True(loadedConfig.LastUsed > loadedConfig.Created);
    }

    public new void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _serviceProvider.Dispose();
        base.Dispose();
    }
}