using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public class ValueFreezeOperations : MemoryScannerBase, IDisposable
    {
        private readonly IValueFreezerService _freezer;
        private bool _disposed;

        public ValueFreezeOperations(
            IValueFreezerService freezer,
            ILogger<ValueFreezeOperations> logger) : base(logger)
        {
            _freezer = freezer;
        }

        public async Task FreezeValue(nint address, byte[] value, string valueType)
        {
            await ExecuteWithLogging(
                () => _freezer.FreezeValue(address, value, valueType),
                "Freezing value",
                address, valueType);
        }

        public async Task UnfreezeValue(nint address)
        {
            await ExecuteWithLogging(
                () => _freezer.UnfreezeValue(address),
                "Unfreezing value",
                address);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _freezer.Dispose();
                _disposed = true;
            }
        }
    }
}