using BlazorFridaApp.Components;
using BlazorFridaApp.Persistence;
using BlazorFridaApp.MemoryScanner;
using BlazorFridaApp.Services;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Security.Principal;
using System.Diagnostics;

// Check if running as administrator (for service status)
#if WINDOWS
bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator);
if (!isAdmin)
{
    Console.WriteLine("Application requires administrator privileges to access process memory.");
}
#endif

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=gamememory.db"));
    
builder.Services.AddScoped<ProcessMemoryScanner>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton<IAdminCheckService, AdminCheckService>();
builder.Services.AddRadzenComponents();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
