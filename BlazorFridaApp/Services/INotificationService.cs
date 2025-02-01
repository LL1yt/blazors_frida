using System;

namespace BlazorFridaApp.Services
{
    public interface INotificationService
    {
        void ShowInfo(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message, Exception? exception = null);
    }
}