using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.Services;

public interface IAppNotificationService
{
    Task ShowSuccess(string message, string? title = null);
    Task ShowError(string message, string? title = null);
    Task ShowWarning(string message, string? title = null);
    Task ShowInfo(string message, string? title = null);
}

public class AppNotificationService : IAppNotificationService
{
    private readonly ILogger<AppNotificationService> _logger;
    
    public AppNotificationService(ILogger<AppNotificationService> logger)
    {
        _logger = logger;
    }

    public Task ShowSuccess(string message, string? title = null)
    {
        _logger.LogInformation("{Title}: {Message}", title ?? "Success", message);
        return Task.CompletedTask;
    }

    public Task ShowError(string message, string? title = null)
    {
        _logger.LogError("{Title}: {Message}", title ?? "Error", message);
        return Task.CompletedTask;
    }

    public Task ShowWarning(string message, string? title = null)
    {
        _logger.LogWarning("{Title}: {Message}", title ?? "Warning", message);
        return Task.CompletedTask;
    }

    public Task ShowInfo(string message, string? title = null)
    {
        _logger.LogInformation("{Title}: {Message}", title ?? "Information", message);
        return Task.CompletedTask;
    }
}