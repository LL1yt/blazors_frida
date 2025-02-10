using Bunit;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace BlazorFridaApp.Tests;

public class TestContextBase : TestContext, IAsyncDisposable
{
    public TestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public new void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public virtual async ValueTask DisposeAsync()
    {
        Dispose();
        await ValueTask.CompletedTask;
    }
}