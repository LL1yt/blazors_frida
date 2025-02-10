using System;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ShowInfo(string title, string message)
    {
        _logger.LogInformation("{Title}: {Message}", title, message);
    }

    public void ShowWarning(string title, string message)
    {
        _logger.LogWarning("{Title}: {Message}", title, message);
    }

    public void ShowError(string title, string message, Exception? exception = null)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "{Title}: {Message}", title, message);
        }
        else
        {
            _logger.LogError("{Title}: {Message}", title, message);
        }
    }

    public void ShowSuccess(string title, string message)
    {
        _logger.LogInformation("{Title}: {Message}", title, message);
    }

    public Task<bool> Confirm(string message, string title)
    {
        _logger.LogInformation("Confirm dialog: {Title} - {Message}", title, message);
        return Task.FromResult(true); // Always confirm in this simple implementation
    }
}