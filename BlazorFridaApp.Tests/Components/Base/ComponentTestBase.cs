using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Blazorise;
using System.Threading.Tasks;

namespace BlazorFridaApp.Tests.Components.Base;

public abstract class ComponentTestBase : TestContext, IAsyncLifetime
{
    protected readonly ILogger Logger;
    protected readonly Dictionary<Type, Mock> Mocks = new();

    protected ComponentTestBase()
    {
        Logger = LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug))
            .CreateLogger(GetType());

        ConfigureServices();
        ConfigureBlazorise();
    }

    protected virtual void ConfigureServices()
    {
        // Add common service mocks
        AddMockService<IProcessService>();
        AddMockService<IMemoryService>();
        AddMockService<IScannerService>();
        AddMockService<IStateService>();
        AddMockService<IFreezeService>();
    }

    protected Mock<T> AddMockService<T>() where T : class
    {
        var mock = new Mock<T>();
        Services.AddScoped<T>(_ => mock.Object);
        Mocks[typeof(T)] = mock;
        return mock;
    }

    protected Mock<T> GetMock<T>() where T : class
    {
        return (Mock<T>)Mocks[typeof(T)];
    }

    private void ConfigureBlazorise()
    {
        Services.AddBlazorise()
            .AddBlazoriseBootstrap();
    }

    protected void AssertComponentRendered<T>(IRenderedComponent<T> component) where T : IComponent
    {
        Assert.NotNull(component);
        Assert.Empty(component.Nodes.Where(n => n.NodeType == NodeType.Text && n.TextContent.Contains("Error")));
    }

    protected async Task AssertNoErrorsLogged(IRenderedComponent<T> component) where T : IComponent
    {
        // Wait for any async operations
        await Task.Delay(100);
        var errors = component.Nodes.Where(n => 
            n.NodeType == NodeType.Text && 
            (n.TextContent.Contains("error", StringComparison.OrdinalIgnoreCase) ||
             n.TextContent.Contains("exception", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        
        Assert.Empty(errors);
    }

    protected void SimulateUserInput(IElement element, string value)
    {
        element.Input(value);
        element.Blur();  // Trigger validation
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}
