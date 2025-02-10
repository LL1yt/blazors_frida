using System;
using Blazorise;

namespace BlazorFridaApp.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationService _notificationService;

    public NotificationService(INotificationService notificationService)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public async Task<bool> Confirm(string message, string? title = null)
    {
        return await _notificationService.Confirm(message, title ?? "Confirm");
    }

    public Task ShowError(string message, string? title = null)
    {
        return _notificationService.Error(message, title ?? "Error");
    }

    public Task ShowInfo(string message, string? title = null)
    {
        return _notificationService.Info(message, title ?? "Information");
    }

    public Task ShowSuccess(string message, string? title = null)
    {
        return _notificationService.Success(message, title ?? "Success");
    }

    public Task ShowWarning(string message, string? title = null)
    {
        return _notificationService.Warning(message, title ?? "Warning");
    }
}