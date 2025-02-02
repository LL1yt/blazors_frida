using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class ValueFreezerService : IValueFreezerService
    {
        private readonly IMemoryReaderService _memoryReader;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ValueFreezerService> _logger;
        private readonly Dictionary<nint, Timer> _freezeTimers = new();
        private bool _disposed;

        public ValueFreezerService(
            IMemoryReaderService memoryReader,
            AppDbContext dbContext,
            ILogger<ValueFreezerService> logger)
        {
            _memoryReader = memoryReader;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task FreezeValue(nint address, byte[] value, string valueType)
        {
            if (address == nint.Zero)
                throw new ArgumentException("Invalid memory address", nameof(address));
            if (value == null || value.Length == 0)
                throw new ArgumentException("Value cannot be null or empty", nameof(value));
            if (string.IsNullOrEmpty(valueType))
                throw new ArgumentException("Value type cannot be null or empty", nameof(valueType));

            try
            {
                _logger.LogInformation("Freezing value at address {Address:X}, type {ValueType}", address, valueType);

                // Stop existing timer if any
                if (_freezeTimers.TryGetValue(address, out var existingTimer))
                {
                    _logger.LogDebug("Stopping existing freeze timer for address {Address:X}", address);
                    existingTimer.Dispose();
                    _freezeTimers.Remove(address);
                }

                // Create new timer for value freezing
                var timer = new Timer(async _ =>
                {
                    try
                    {
                        await _memoryReader.WriteMemoryBytes(address, value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during freeze timer callback for address {Address:X}", address);
                    }
                }, null, 0, 100); // Update every 100ms for more responsive freezing

                _freezeTimers[address] = timer;
                _logger.LogDebug("Started new freeze timer for address {Address:X}", address);

                try
                {
                    // Update or create locked address record
                    var lockedAddress = await _dbContext.LockedAddresses
                        .FirstOrDefaultAsync(la => la.Address == (long)address);

                    if (lockedAddress == null)
                    {
                        _logger.LogDebug("Creating new locked address record for {Address:X}", address);
                        var process = Process.GetProcessById(_memoryReader.ProcessHandle.ToInt32());
                        var originalBytes = await _memoryReader.ReadMemoryBytes(address, value.Length);

                        lockedAddress = new LockedAddress
                        {
                            ProcessName = process.ProcessName,
                            Address = (long)address,
                            ValueType = valueType,
                            OriginalBytes = originalBytes,
                            CurrentValue = value,
                            IsFrozen = true,
                            LastAccessed = DateTime.UtcNow
                        };
                        await _dbContext.LockedAddresses.AddAsync(lockedAddress);
                        _logger.LogInformation("Created new locked address record for {Address:X}", address);
                    }
                    else
                    {
                        _logger.LogDebug("Updating existing locked address record for {Address:X}", address);
                        lockedAddress.CurrentValue = value;
                        lockedAddress.ValueType = valueType;
                        lockedAddress.IsFrozen = true;
                        lockedAddress.LastAccessed = DateTime.UtcNow;
                    }

                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Successfully saved locked address record for {Address:X}", address);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error while freezing value at {Address:X}", address);
                    timer.Dispose();
                    _freezeTimers.Remove(address);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to freeze value at address {Address:X}", address);
                throw;
            }
        }

        public async Task UnfreezeValue(nint address)
        {
            if (address == nint.Zero)
                throw new ArgumentException("Invalid memory address", nameof(address));

            try
            {
                _logger.LogInformation("Unfreezing value at address {Address:X}", address);

                if (_freezeTimers.TryGetValue(address, out var timer))
                {
                    _logger.LogDebug("Stopping freeze timer for address {Address:X}", address);
                    timer.Dispose();
                    _freezeTimers.Remove(address);

                    try
                    {
                        var lockedAddress = await _dbContext.LockedAddresses
                            .FirstOrDefaultAsync(la => la.Address == (long)address);

                        if (lockedAddress != null)
                        {
                            // Restore original value if available
                            if (lockedAddress.OriginalBytes.Length > 0)
                            {
                                _logger.LogDebug("Restoring original value at address {Address:X}", address);
                                await _memoryReader.WriteMemoryBytes(address, lockedAddress.OriginalBytes);
                            }

                            lockedAddress.IsFrozen = false;
                            lockedAddress.LastAccessed = DateTime.UtcNow;
                            await _dbContext.SaveChangesAsync();
                            _logger.LogInformation("Successfully unfroze value at address {Address:X}", address);
                        }
                        else
                        {
                            _logger.LogWarning("No locked address record found for {Address:X}", address);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Database error while unfreezing value at {Address:X}", address);
                        throw;
                    }
                }
                else
                {
                    _logger.LogWarning("No freeze timer found for address {Address:X}", address);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unfreeze value at address {Address:X}", address);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                foreach (var timer in _freezeTimers.Values)
                {
                    timer.Dispose();
                }
                _freezeTimers.Clear();
                _disposed = true;

                GC.SuppressFinalize(this);
            }
        }
    }
}