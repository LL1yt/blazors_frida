using BlazorFridaApp.Components;
using BlazorFridaApp.MemoryScanner;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using BlazorFridaApp.Services;
using BlazorFridaApp.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Serilog;
using Serilog.Events;
using BlazorFridaApp.MemoryScanner.Base;

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

    // Add assets service
    builder.Services.AddScoped<AssetsService>();

    // Add Radzen services
    builder.Services.AddRadzenComponents();
    builder.Services.AddScoped<DialogService>();
    builder.Services.AddScoped<Radzen.NotificationService>();

    // Add notification service
    builder.Services.AddScoped<INotificationService, AppNotificationService>();

    // Add memory scanner services
    builder.Services.AddSingleton<IPythonRuntimeService, PythonRuntimeService>(); // Python runtime singleton
    builder.Services.AddScoped<IFridaInteropService, FridaInteropService>(); // Frida interop service
    builder.Services.AddScoped<IProcessService, FridaProcessService>();
    builder.Services.AddScoped<IMemoryReaderService, FridaMemoryService>();
    builder.Services.AddScoped<IMemoryScannerService, FridaMemoryScannerService>();
    builder.Services.AddScoped<IValueFreezerService, ValueFreezerService>();
    builder.Services.AddScoped<IScanProfileService, ScanProfileService>();

    // Add the main ProcessMemoryScanner that orchestrates all services
    builder.Services.AddScoped<IProcessMemoryScanner, ProcessMemoryScanner>();

    // Add the MemoryScanner page service
    builder.Services.AddScoped<MemoryScannerService>();

    // Add database context and initialization service
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=gamememory.db",
            sqliteOptions => sqliteOptions.MigrationsAssembly("BlazorFridaApp")));
    builder.Services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();
var app = builder.Build();

// Initialize database
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
    await dbInitService.InitializeDatabaseAsync().ConfigureAwait(false);
}

// Configure the HTTP request pipeline.
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Global exception handler
    AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
    {
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
