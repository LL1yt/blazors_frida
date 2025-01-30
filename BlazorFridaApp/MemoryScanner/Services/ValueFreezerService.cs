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
            if (_freezeTimers.TryGetValue(address, out var existingTimer))
            {
                existingTimer.Dispose();
                _freezeTimers.Remove(address);
            }

            var timer = new Timer(async _ =>
            {
                await _memoryReader.WriteMemoryBytes(address, value);
            }, null, 0, 100); // Update every 100ms for more responsive freezing

            _freezeTimers[address] = timer;

            // Update or create locked address record
            var lockedAddress = await _dbContext.LockedAddresses
                .FirstOrDefaultAsync(la => la.Address == (long)address);

            if (lockedAddress == null)
            {
                lockedAddress = new LockedAddress
                {
                    ProcessName = Process.GetProcessById(_memoryReader.ProcessHandle.ToInt32()).ProcessName,
                    Address = (long)address,
                    ValueType = valueType,
                    OriginalBytes = await _memoryReader.ReadMemoryBytes(address, value.Length),
                    CurrentValue = value,
                    IsFrozen = true,
                    LastAccessed = DateTime.UtcNow
                };
                await _dbContext.LockedAddresses.AddAsync(lockedAddress);
            }
            else
            {
                lockedAddress.CurrentValue = value;
                lockedAddress.ValueType = valueType;
                lockedAddress.IsFrozen = true;
                lockedAddress.LastAccessed = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UnfreezeValue(nint address)
        {
            if (_freezeTimers.TryGetValue(address, out var timer))
            {
                timer.Dispose();
                _freezeTimers.Remove(address);

                var lockedAddress = await _dbContext.LockedAddresses
                    .FirstOrDefaultAsync(la => la.Address == (long)address);

                if (lockedAddress != null)
                {
                    // Restore original value if available
                    if (lockedAddress.OriginalBytes.Length > 0)
                    {
                        await _memoryReader.WriteMemoryBytes(address, lockedAddress.OriginalBytes);
                    }

                    lockedAddress.IsFrozen = false;
                    lockedAddress.LastAccessed = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var timer in _freezeTimers.Values)
                {
                    timer.Dispose();
                }
                _freezeTimers.Clear();
                _disposed = true;
            }
        }
    }
}