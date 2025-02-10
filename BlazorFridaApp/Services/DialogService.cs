using Microsoft.AspNetCore.Components;
using Blazorise;

namespace BlazorFridaApp.Services;

public class DialogService
{
    private readonly INotificationService _notificationService;

    public DialogService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task<bool> Confirm(string message, string? title = null)
    {
        return _notificationService.Confirm(message, title ?? "Confirm");
    }

    public void Close(object? result = null)
    {
        // This is now handled by the Modal component directly
    }
}