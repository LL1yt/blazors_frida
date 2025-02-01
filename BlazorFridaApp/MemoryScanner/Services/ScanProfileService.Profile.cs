using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public partial class ScanProfileService
    {
        public ScanProfile GetCurrentProfile()
        {
            return new ScanProfile
            {
                Name = "Default Profile",
                ProcessName = "Unknown",
                Pattern = new byte[0],
                Mask = "",
                Offsets = new int[0],
                Created = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };
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
    }
}