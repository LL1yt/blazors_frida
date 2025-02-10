using Bunit;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BlazorFridaApp.Tests;

public class TestContextBase : TestContext
{
    public TestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public new void Dispose()
    {
        base.Dispose();
    }
}