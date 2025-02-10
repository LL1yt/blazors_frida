using System;
using Blazorise;

namespace BlazorFridaApp.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationService<string> _notificationService;

    public NotificationService(NotificationService<string> notificationService)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public void ShowInfo(string title, string message)
    {
        _notificationService.Info(title, message);
    }

    public void ShowWarning(string title, string message)
    {
        _notificationService.Warning(title, message);
    }

    public void ShowError(string title, string message, Exception? exception = null)
    {
        _notificationService.Error(title, message);
    }

    public void ShowSuccess(string title, string message)
    {
        _notificationService.Success(title, message);
    }

    public Task<bool> Confirm(string message, string title)
    {
        return Task.FromResult(true); // Temporary implementation until we add proper dialog service
    }
}