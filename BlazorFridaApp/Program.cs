using BlazorFridaApp.Components;
using BlazorFridaApp.MemoryScanner;
using BlazorFridaApp.MemoryScanner.Services;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using BlazorFridaApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Serilog;
using Serilog.Events;

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

    builder.Services.AddRadzenComponents();

    // Add database context
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=gamememory.db"));

    // Add memory scanner services
    builder.Services.AddScoped<IProcessService, FridaProcessService>(); // Changed to FridaProcessService
    builder.Services.AddScoped<IMemoryReaderService, FridaMemoryService>(); // Using FridaMemoryService
    builder.Services.AddScoped<IValueFreezerService, ValueFreezerService>();
    builder.Services.AddScoped<IScanProfileService, ScanProfileService>();

    // Add the main ProcessMemoryScanner that orchestrates all services
    builder.Services.AddScoped<ProcessMemoryScanner>();

    var app = builder.Build();

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
