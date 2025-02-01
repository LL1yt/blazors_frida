using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public partial class ScanProfileService
    {
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