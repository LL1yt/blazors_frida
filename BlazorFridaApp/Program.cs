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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add assets service
builder.Services.AddScoped<AssetsService>();

builder.Services.AddRadzenComponents();

// Add database context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=gamememory.db"));

// Add admin check service
builder.Services.AddScoped<AdminCheckService>();

// Add memory scanner services
builder.Services.AddScoped<IProcessService, ProcessService>();
builder.Services.AddScoped<IMemoryReaderService, MemoryReaderService>();
builder.Services.AddScoped<IMemoryScannerService, MemoryScannerService>();
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

app.Run();
