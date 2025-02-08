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

        public async Task<ScanProfile> SaveProfileAsync(ScanProfile profile)
        {
            try
            {
                _logger.LogInformation("Saving scan profile: {ProfileName}", profile.Name);

                var existingProfile = await _dbContext.ScanProfiles
                    .FirstOrDefaultAsync(p => p.Name == profile.Name);

                if (existingProfile != null)
                {
                    existingProfile.ProcessName = profile.ProcessName;
                    existingProfile.Pattern = profile.Pattern;
                    existingProfile.Mask = profile.Mask;
                    existingProfile.Offsets = profile.Offsets;
                    existingProfile.ComparisonType = profile.ComparisonType;
                    existingProfile.LastUsed = DateTime.UtcNow;
                    _dbContext.ScanProfiles.Update(existingProfile);
                }
                else
                {
                    profile.Created = DateTime.UtcNow;
                    profile.LastUsed = DateTime.UtcNow;
                    await _dbContext.ScanProfiles.AddAsync(profile);
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully saved scan profile {ProfileName}", profile.Name);
                return existingProfile ?? profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save scan profile {ProfileName}", profile.Name);
                throw;
            }
        }

        public async Task<ScanProfile> GetProfileAsync(string name)
        {
            try
            {
                _logger.LogInformation("Retrieving scan profile: {ProfileName}", name);

                var profile = await _dbContext.ScanProfiles
                    .FirstOrDefaultAsync(p => p.Name == name);

                if (profile == null)
                {
                    _logger.LogWarning("Scan profile not found: {ProfileName}", name);
                    throw new KeyNotFoundException($"Profile '{name}' not found");
                }

                profile.LastUsed = DateTime.UtcNow;
                _dbContext.ScanProfiles.Update(profile);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully retrieved scan profile {ProfileName}", name);
                return profile;
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Failed to retrieve scan profile {ProfileName}", name);
                throw;
            }
        }

        public List<string> ValidateProfile(ScanProfile profile)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(profile.Name))
                errors.Add("Profile name is required");

            if (string.IsNullOrEmpty(profile.ProcessName))
                errors.Add("Process name is required");

            if (profile.Pattern.Length > 256)
                errors.Add("Pattern is too long (maximum 256 bytes)");

            if (!string.IsNullOrEmpty(profile.Mask) && profile.Mask.Length != profile.Pattern.Length)
                errors.Add("Mask length must match pattern length");

            if (profile.Offsets.Length > 16)
                errors.Add("Too many offsets (maximum 16)");

            if (!string.IsNullOrEmpty(profile.ComparisonType) && 
                !new[] { "exact", "fuzzy", "greater", "less", "between" }.Contains(profile.ComparisonType.ToLower()))
                errors.Add("Invalid comparison type");

            return errors;
        }
    }
}