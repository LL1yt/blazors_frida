using BlazorFridaApp.Components;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.MemoryScanner.Services.Decorators;
using BlazorFridaApp.MemoryScanner.Middleware;
using BlazorFridaApp.MemoryScanner;
using BlazorFridaApp.MemoryScanner.Configuration;
using BlazorFridaApp.Persistence;
using BlazorFridaApp.Services;
using BlazorFridaApp.Services.Interfaces;
using BlazorFridaApp.Components.Pages;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Serilog;
using Serilog.Events;
using BlazorFridaApp.MemoryScanner.Base;
using Microsoft.Extensions.Http;
using Scrutor;
using Microsoft.Extensions.Options;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Blazorise.RichTextEdit;
using Blazorise.LoadingIndicator;
using Blazorise.Sidebar;
using Blazorise.Snackbar;
using Blazorise.TreeView;

// Setup Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Serilog to the application
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Add Blazorise
    builder.Services
        .AddBlazorise(options =>
        {
            options.Immediate = true;
        })
        .AddBootstrapProviders()
        .AddFontAwesomeIcons()
        .AddBlazoriseRichTextEdit()
        .AddBlazoriseTreeView()
        .AddBlazoriseSnackbar()
        .AddBlazoriseDataGrid();

    // Add assets service
    builder.Services.AddScoped<AssetsService>();

    // Add notification and dialog services
    builder.Services.AddScoped<NotificationService<string>>();
    builder.Services.AddScoped<BlazorFridaApp.Services.INotificationService, BlazorFridaApp.Services.NotificationService>();
    builder.Services.AddScoped<BlazorFridaApp.Services.DialogService>();

    // Configure OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracerProvider =>
        {
            tracerProvider
                .AddSource("BlazorFridaApp")
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("BlazorFridaApp"))
                .AddGrpcClientInstrumentation();
        })
        .WithMetrics(metricsProvider =>
        {
            metricsProvider
                .AddMeter("BlazorFridaApp")
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("BlazorFridaApp"));
        });

    // Add Python process manager
    builder.Services.AddSingleton<IPythonProcessManager>(sp => 
        new PythonProcessManager(sp.GetRequiredService<ILogger<PythonProcessManager>>(), 50051));

    // Register specialized gRPC services
    builder.Services.AddScoped<ProcessGrpcService>();
    builder.Services.AddScoped<MemoryGrpcService>();
    builder.Services.AddScoped<ScannerGrpcService>();
    builder.Services.AddScoped<StateGrpcService>();
    builder.Services.AddScoped<FreezeGrpcService>();

    // Register service interfaces
    builder.Services.AddScoped<IProcessGrpcService, ProcessGrpcService>();
    builder.Services.AddScoped<IMemoryGrpcService, MemoryGrpcService>();
    builder.Services.AddScoped<IScannerGrpcService, ScannerGrpcService>();
    builder.Services.AddScoped<IStateGrpcService, StateGrpcService>();
    builder.Services.AddScoped<IFreezeGrpcService, FreezeGrpcService>();
    builder.Services.AddScoped<IScanProfileService, ScanProfileService>();

    // Register memory scanner services
    builder.Services.AddScoped<MemoryScannerGrpcService>();
    builder.Services.AddScoped<MemoryScannerFacade>();
    builder.Services.AddScoped<IMemoryScannerGrpcService>(sp => sp.GetRequiredService<MemoryScannerFacade>());
    builder.Services.AddScoped<IMemoryScannerService>(sp => (IMemoryScannerService)sp.GetRequiredService<MemoryScannerFacade>());
    builder.Services.AddScoped<IValueFreezerService, ValueFreezerService>();

    // Register process and memory services
    builder.Services.AddScoped<IProcessService, ProcessGrpcService>();
    builder.Services.AddScoped<IMemoryReaderService, MemoryGrpcService>();
    builder.Services.Decorate<IMemoryReaderService, RetryMemoryServiceDecorator>();

    // Add the main ProcessMemoryScanner that orchestrates all services
    builder.Services.AddScoped<IProcessMemoryScanner, ProcessMemoryScanner>();

    // Add the MemoryScanner page service
    builder.Services.AddScoped<MemoryScannerService>();

    // Add database context and initialization service
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        options.UseSqlite("Data Source=gamememory.db",
            sqliteOptions => 
            {
                if (sqliteOptions == null)
                    throw new ArgumentNullException(nameof(sqliteOptions));
                    
                sqliteOptions.MigrationsAssembly("BlazorFridaApp");
            });
    });
    builder.Services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();

    // Add memory cleanup service
    builder.Services.AddScoped<IMemoryCleanupService, MemoryCleanupService>();

    // Add feature flag service
    builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<GrpcHealthCheck>("grpc_health_check", tags: new[] { "grpc" });

    builder.Services.AddScoped<RetryPolicyService>();
    builder.Services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
        logging.SetMinimumLevel(LogLevel.Information);
    });

    // Add configuration
    builder.Services.Configure<MemoryScannerSettings>(
        builder.Configuration.GetSection("MemoryScanner"));

    // Add configuration validation
    builder.Services.AddSingleton<IValidateOptions<MemoryScannerSettings>, ConfigurationValidator>();

    // Validate configuration at startup
    var memoryScannerSettings = builder.Configuration
        .GetSection("MemoryScanner")
        .Get<MemoryScannerSettings>();

    if (memoryScannerSettings == null)
    {
        throw new InvalidOperationException("MemoryScanner configuration section is missing");
    }

    var validator = new ConfigurationValidator();
    var validationResult = validator.Validate(null, memoryScannerSettings);

    if (validationResult.Failed)
    {
        throw new InvalidOperationException(
            $"MemoryScanner configuration validation failed: {string.Join(", ", validationResult.Failures)}");
    }

    var app = builder.Build();

    // Configure ProcessInfo logger
    using (var scope = app.Services.CreateScope())
    {
        var processInfoLogger = scope.ServiceProvider.GetRequiredService<ILogger<ProcessInfo>>();
        ProcessInfo.ConfigureLogger(processInfoLogger);
    }

    // Initialize database
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
        await dbInitService.InitializeDatabaseAsync().ConfigureAwait(false);
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Map health checks
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            context.Response.ContentType = "application/json";
            var result = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key ?? "Unknown",
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description ?? string.Empty,
                    duration = e.Value.Duration.ToString()
                })
            };
            await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, result);
        }
    });

    // Global exception handler
    AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
    {
        if (error?.ExceptionObject == null)
            return;

        Log.Fatal(error.ExceptionObject as Exception, "Unhandled application error");
    };

    try
    {
        Log.Information("Starting web application");
        app.Run();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
    }
    finally
    {
        Log.CloseAndFlush();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
