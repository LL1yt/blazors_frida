using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class ScanProfileService : IScanProfileService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ScanProfileService> _logger;

        public ScanProfileService(AppDbContext dbContext, ILogger<ScanProfileService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task SaveScanResults(int processId, byte[] pattern, string mask, List<nint> addresses)
        {
            var processName = Process.GetProcessById(processId).ProcessName;
            var profile = new ScanProfile
            {
                Name = $"{processName} Scan {DateTime.Now:yyyyMMdd-HHmmss}",
                ProcessName = processName,
                Pattern = pattern,
                Mask = mask,
                Offsets = addresses.Any() ? 
                    addresses.Select(a => (int)(a - addresses.First())).ToArray() : Array.Empty<int>(),
                Created = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            await _dbContext.ScanProfiles.AddAsync(profile);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Saved scan results for process {ProcessId} with {MatchCount} matches", 
                processId, addresses.Count);
        }

        public async Task SaveLastProcess(int processId)
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(a => a.Key == "LastProcess");
            
            if (setting == null)
            {
                setting = new ApplicationSetting { Key = "LastProcess" };
                await _dbContext.AddAsync(setting);
            }

            setting.Value = processId.ToString();
            setting.LastModified = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        public async Task<int?> GetLastProcessId()
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(a => a.Key == "LastProcess");
            
            if (setting != null && int.TryParse(setting.Value, out int processId))
            {
                return processId;
            }
            return null;
        }
    }
}