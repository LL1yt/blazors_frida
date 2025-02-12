using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BlazorFridaApp.Tests.Infrastructure;

public abstract class TestBase : TestContext
{
    protected readonly TestContext Context;

    protected TestBase()
    {
        Context = new TestContext();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Add any common services here
        Context.Services.AddScoped<IServiceProvider>(sp => sp);
        
        // Configure services
        ConfigureServices(Context.Services);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Override in derived classes to add specific services
    }
} 