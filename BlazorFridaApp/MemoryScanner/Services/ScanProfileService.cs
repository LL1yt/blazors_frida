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
            if (processId <= 0)
                throw new ArgumentException("Invalid process ID", nameof(processId));
            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
            if (string.IsNullOrEmpty(mask))
                throw new ArgumentException("Mask cannot be null or empty", nameof(mask));
            if (addresses == null)
                throw new ArgumentException("Addresses list cannot be null", nameof(addresses));

            try
            {
                _logger.LogInformation("Saving scan results for process {ProcessId} with {MatchCount} matches",
                    processId, addresses.Count);

                Process process;
                try
                {
                    process = Process.GetProcessById(processId);
                    _logger.LogDebug("Retrieved process info for {ProcessName} ({ProcessId})",
                        process.ProcessName, processId);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "Process {ProcessId} not found", processId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accessing process {ProcessId}", processId);
                    throw;
                }

                var profile = new ScanProfile
                {
                    Name = $"{process.ProcessName} Scan {DateTime.Now:yyyyMMdd-HHmmss}",
                    ProcessName = process.ProcessName,
                    Pattern = pattern,
                    Mask = mask,
                    Offsets = addresses.Any() ?
                        addresses.Select(a => (int)(a - addresses.First())).ToArray() : System.Array.Empty<int>(),
                    Created = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };

                try
                {
                    await _dbContext.ScanProfiles.AddAsync(profile);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Successfully saved scan profile for {ProcessName}", process.ProcessName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error while saving scan profile for process {ProcessId}", processId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save scan results for process {ProcessId}", processId);
                throw;
            }
        }

        public async Task SaveLastProcess(int processId)
        {
            if (processId <= 0)
                throw new ArgumentException("Invalid process ID", nameof(processId));

            try
            {
                _logger.LogInformation("Saving last process ID: {ProcessId}", processId);

                try
                {
                    // Verify process exists
                    var process = Process.GetProcessById(processId);
                    _logger.LogDebug("Verified process exists: {ProcessName} ({ProcessId})",
                        process.ProcessName, processId);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "Process {ProcessId} not found", processId);
                    throw;
                }

                try
                {
                    var setting = await _dbContext.ApplicationSettings
                        .FirstOrDefaultAsync(a => a.Key == "LastProcess");
                    
                    if (setting == null)
                    {
                        _logger.LogDebug("Creating new LastProcess setting");
                        setting = new ApplicationSetting { Key = "LastProcess" };
                        await _dbContext.AddAsync(setting);
                    }

                    setting.Value = processId.ToString();
                    setting.LastModified = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Successfully saved last process ID");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error while saving last process ID");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save last process ID");
                throw;
            }
        }

        public async Task<int?> GetLastProcessId()
        {
            try
            {
                _logger.LogInformation("Retrieving last process ID");

                try
                {
                    var setting = await _dbContext.ApplicationSettings
                        .FirstOrDefaultAsync(a => a.Key == "LastProcess");
                    
                    if (setting == null)
                    {
                        _logger.LogInformation("No last process ID found");
                        return null;
                    }

                    if (int.TryParse(setting.Value, out int processId))
                    {
                        _logger.LogInformation("Retrieved last process ID: {ProcessId}", processId);
                        return processId;
                    }
                    else
                    {
                        _logger.LogWarning("Invalid process ID format in database: {Value}", setting.Value);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error while retrieving last process ID");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get last process ID");
                throw;
            }
        }
    }
}