using System;
using Radzen;

namespace BlazorFridaApp.Services
{
    public class AppNotificationService : INotificationService
    {
        private readonly DialogService _dialogService;

        public AppNotificationService(DialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public void ShowInfo(string title, string message)
        {
            _dialogService.Alert(message, title, new AlertOptions
            {
                OkButtonText = "OK"
            });
        }

        public void ShowWarning(string title, string message)
        {
            _dialogService.Alert(message, title, new AlertOptions
            {
                OkButtonText = "OK"
            });
        }

        public void ShowError(string title, string message, Exception? exception = null)
        {
            var detail = exception != null ? $"{message}\n{exception.Message}" : message;
            _dialogService.Alert(detail, title, new AlertOptions
            {
                OkButtonText = "OK"
            });
        }

        public void ShowSuccess(string title, string message)
        {
            _dialogService.Alert(message, title, new AlertOptions
            {
                OkButtonText = "OK"
            });
        }
    }
}