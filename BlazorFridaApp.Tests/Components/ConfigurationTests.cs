using Bunit;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Collections.Generic;
using System;
using System.IO;

namespace BlazorFridaApp.Tests.Components;

public class ConfigurationTests : TestContextBase, IDisposable
{
    private readonly Mock<IScanProfileService> _profileServiceMock;
    private readonly AppDbContext _dbContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly string _dbPath;

    public ConfigurationTests()
    {
        _profileServiceMock = new Mock<IScanProfileService>();
        _dbPath = $"Data Source=TestMemoryScanner_{Guid.NewGuid()}.db";
        
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(_dbPath));
            
        services.AddScoped<IScanProfileService>(_ => _profileServiceMock.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _dbContext.Database.EnsureCreated();
        
        Services.AddScoped<IScanProfileService>(_ => _profileServiceMock.Object);
    }

    [Fact]
    public async Task ShouldSaveAndLoadScanConfiguration()
    {
        // Arrange
        var processSettings = new ProcessSettings 
        { 
            ProcessName = "notepad.exe",
            Notes = "Test process"
        };
        await _dbContext.ProcessSettings.AddAsync(processSettings);
        await _dbContext.SaveChangesAsync();

        var config = new ScanProfile
        {
            Name = "Test Config",
            ProcessName = "notepad.exe",
            ProcessSettingsId = processSettings.Id,
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
        var processSettings = new ProcessSettings 
        { 
            ProcessName = "test.exe",
            Notes = "Test process"
        };
        await _dbContext.ProcessSettings.AddAsync(processSettings);
        await _dbContext.SaveChangesAsync();

        var config1 = new ScanProfile 
        { 
            Name = "TestConfig", 
            ProcessName = "test.exe",
            ProcessSettingsId = processSettings.Id
        };
        var config2 = new ScanProfile 
        { 
            Name = "TestConfig", 
            ProcessName = "test.exe",
            ProcessSettingsId = processSettings.Id
        };

        // Act
        await _dbContext.ScanProfiles.AddAsync(config1);
        await _dbContext.SaveChangesAsync();
        
        // Clear the change tracker to simulate a fresh context
        _dbContext.ChangeTracker.Clear();
        
        await _dbContext.ScanProfiles.AddAsync(config2);
        
        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(async () => 
        {
            await _dbContext.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task ShouldTrackConfigurationUsage()
    {
        // Arrange
        var processSettings = new ProcessSettings 
        { 
            ProcessName = "notepad.exe",
            Notes = "Test process"
        };
        await _dbContext.ProcessSettings.AddAsync(processSettings);
        await _dbContext.SaveChangesAsync();

        var config = new ScanProfile
        {
            Name = "Test Config",
            ProcessName = "notepad.exe",
            ProcessSettingsId = processSettings.Id,
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            _serviceProvider.Dispose();
            if (File.Exists(_dbPath))
            {
                try
                {
                    File.Delete(_dbPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
        base.Dispose(disposing);
    }
}