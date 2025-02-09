using Bunit;
using System;

namespace BlazorFridaApp.Tests;

public class TestContextWrapper : BunitContext, IAsyncDisposable, IDisposable
{
    private bool _disposed;

    public new void Dispose()
    {
        if (!_disposed)
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            _disposed = true;
            base.Dispose();
        }
    }

    public new async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await base.DisposeAsync();
            _disposed = true;
        }
    }
}