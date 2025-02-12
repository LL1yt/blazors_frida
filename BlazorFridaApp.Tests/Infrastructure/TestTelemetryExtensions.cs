using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Context.Propagation;

namespace BlazorFridaApp.Tests.Infrastructure;

public static class TestTelemetryExtensions
{
    public static IServiceCollection AddTestTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .AddSource("BlazorFridaApp.Tests")
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("BlazorFridaApp.Tests"))
                .AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        // Add propagator for distributed tracing in tests
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

        return services;
    }
}