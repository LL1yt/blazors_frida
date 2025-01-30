using BlazorFridaApp.Components;
using BlazorFridaApp.Persistence;
using BlazorFridaApp.MemoryScanner;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Security.Principal;
using System.Diagnostics;

// Проверяем права администратора
#if WINDOWS
bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator);

if (!isAdmin)
{
    // Restart the application with admin rights
    var startInfo = new ProcessStartInfo
    {
        UseShellExecute = true,
        WorkingDirectory = Environment.CurrentDirectory,
        FileName = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
        Verb = "runas" // This requests elevation
    };

    try
    {
        Process.Start(startInfo);
        return; // Exit current process
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Console.WriteLine("User declined elevation. The application requires administrator privileges to access process memory.");
        return;
    }
}
#endif

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=gamememory.db"));
    
builder.Services.AddScoped<ProcessMemoryScanner>();
builder.Services.AddScoped<NotificationService>();
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
