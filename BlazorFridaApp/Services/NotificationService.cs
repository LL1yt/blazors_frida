using System;
using Blazorise;

namespace BlazorFridaApp.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationService _notificationService;

    public NotificationService(INotificationService(notificationService))
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public async Task Show(NotificationMessage message)
    {
        await _notificationService.Show(message);
    }

    public void ShowInfo(string title, string message)
    {
        Show(new NotificationMessage { Title = title, Message = message, NotificationType = NotificationType.Info }).Wait();
    }

    public void ShowWarning(string title, string message)
    {
        Show(new NotificationMessage { Title = title, Message = message, NotificationType = NotificationType.Warning }).Wait();
    }

    public void ShowError(string title, string message, Exception? exception = null)
    {
        Show(new NotificationMessage { Title = title, Message = message, NotificationType = NotificationType.Error }).Wait();
    }

    public void ShowSuccess(string title, string message)
    {
        Show(new NotificationMessage { Title = title, Message = message, NotificationType = NotificationType.Success }).Wait();
    }

    public Task<bool> Confirm(string message, string title)
    {
        return Task.FromResult(true); // Temporary implementation until we add proper dialog service
    }
}