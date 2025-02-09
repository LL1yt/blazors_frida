using BlazorFridaApp.Components.Pages;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public partial class ScanProfileService : IScanProfileService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ScanProfileService> _logger;

        public ScanProfileService(AppDbContext dbContext, ILogger<ScanProfileService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, Models.ScannerConfig>> GetScannerConfigs()
        {
            try
            {
                var configs = await _dbContext.ScannerConfigs.ToDictionaryAsync(c => c.Name, c => c);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get scanner configs");
                return new Dictionary<string, Models.ScannerConfig>();
            }
        }

        public async Task SaveScannerConfig(string name, Models.ScannerConfig config)
        {
            try
            {
                var existingConfig = await _dbContext.ScannerConfigs.FindAsync(name);
                if (existingConfig != null)
                {
                    _dbContext.Entry(existingConfig).CurrentValues.SetValues(config);
                }
                else
                {
                    config.Name = name;
                    await _dbContext.ScannerConfigs.AddAsync(config);
                }
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save scanner config {Name}", name);
                throw;
            }
        }

        public async Task DeleteScannerConfig(string name)
        {
            try
            {
                var config = await _dbContext.ScannerConfigs.FindAsync(name);
                if (config != null)
                {
                    _dbContext.ScannerConfigs.Remove(config);
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete scanner config {Name}", name);
                throw;
            }
        }

        public async Task SaveLastProcess(int processId)
        {
            try
            {
                var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == "LastProcessId");
                if (setting != null)
                {
                    setting.Value = processId.ToString();
                }
                else
                {
                    await _dbContext.Settings.AddAsync(new Setting { Key = "LastProcessId", Value = processId.ToString() });
                }
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save last process ID {ProcessId}", processId);
                throw;
            }
        }

        public async Task<int?> GetLastProcessId()
        {
            try
            {
                var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == "LastProcessId");
                if (setting != null && int.TryParse(setting.Value, out int processId))
                {
                    return processId;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get last process ID");
                return null;
            }
        }

        public async Task SaveScanResults(IEnumerable<ScanResult> results)
        {
            try
            {
                await _dbContext.ScanResults.AddRangeAsync(results);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save scan results");
                throw;
            }
        }

        public async Task<ScanProfile> SaveProfileAsync(ScanProfile profile)
        {
            try
            {
                var existingProfile = await _dbContext.ScanProfiles.FindAsync(profile.Name);
                if (existingProfile != null)
                {
                    _dbContext.Entry(existingProfile).CurrentValues.SetValues(profile);
                }
                else
                {
                    await _dbContext.ScanProfiles.AddAsync(profile);
                }
                await _dbContext.SaveChangesAsync();
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save scan profile {Name}", profile.Name);
                throw;
            }
        }

        public async Task<ScanProfile> GetProfileAsync(string name)
        {
            try
            {
                return await _dbContext.ScanProfiles.FindAsync(name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get scan profile {Name}", name);
                throw;
            }
        }

        public List<string> ValidateProfile(ScanProfile profile)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                errors.Add("Profile name is required");
            }

            if (profile.ValueType == MemoryValueType.Unknown)
            {
                errors.Add("Value type must be specified");
            }

            if (string.IsNullOrWhiteSpace(profile.ComparisonType))
            {
                errors.Add("Comparison type must be specified");
            }

            return errors;
        }
    }
}