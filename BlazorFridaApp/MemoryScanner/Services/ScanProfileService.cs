using BlazorFridaApp.Components.Pages;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public partial class ScanProfileService : IScanProfileService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ScanProfileService> _logger;

        public ScanProfileService(AppDbContext dbContext, ILogger<ScanProfileService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Dictionary<string, ScannerConfig>> GetScannerConfigs()
        {
            var settings = await _dbContext.ApplicationSettings
                .Where(s => s.Type == "ScannerConfig")
                .ToListAsync();

            var configs = new Dictionary<string, ScannerConfig>();
            foreach (var setting in settings)
            {
                var config = JsonSerializer.Deserialize<ScannerConfig>(setting.Value);
                if (config != null)
                {
                    configs[setting.Key] = config;
                }
            }

            return configs;
        }

        public async Task SaveScannerConfig(string name, ScannerConfig config)
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Type == "ScannerConfig" && s.Key == name);

            if (setting == null)
            {
                setting = new ApplicationSetting
                {
                    Type = "ScannerConfig",
                    Key = name,
                    Value = JsonSerializer.Serialize(config)
                };
                _dbContext.ApplicationSettings.Add(setting);
            }
            else
            {
                setting.Value = JsonSerializer.Serialize(config);
                _dbContext.ApplicationSettings.Update(setting);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteScannerConfig(string name)
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Type == "ScannerConfig" && s.Key == name);

            if (setting != null)
            {
                _dbContext.ApplicationSettings.Remove(setting);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task SaveScanResults(IEnumerable<ScanResult> results)
        {
            foreach (var result in results)
            {
                await SaveScanResults(result.ProcessId, result.Pattern, result.Mask, result.Addresses);
            }
        }
    }
}