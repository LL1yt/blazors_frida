using Microsoft.AspNetCore.Components;
using Blazorise;

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
    private readonly Blazorise.INotificationService _notificationService;
    
    public AppNotificationService(Blazorise.INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task ShowSuccess(string message, string? title = null)
    {
        return _notificationService.Success(title ?? "Success", message);
    }

    public Task ShowError(string message, string? title = null)
    {
        return _notificationService.Error(title ?? "Error", message);
    }

    public Task ShowWarning(string message, string? title = null)
    {
        return _notificationService.Warning(title ?? "Warning", message);
    }

    public Task ShowInfo(string message, string? title = null)
    {
        return _notificationService.Info(title ?? "Information", message);
    }
}