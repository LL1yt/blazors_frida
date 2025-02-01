using System;
using Radzen;

namespace BlazorFridaApp.Services
{
    public class NotificationService : INotificationService
    {
        private readonly NotificationService _radzenNotificationService;

        public NotificationService(NotificationService radzenNotificationService)
        {
            _radzenNotificationService = radzenNotificationService;
        }

        public void ShowInfo(string title, string message)
        {
            _radzenNotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = title,
                Detail = message,
                Duration = 4000
            });
        }

        public void ShowWarning(string title, string message)
        {
            _radzenNotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = title,
                Detail = message,
                Duration = 4000
            });
        }

        public void ShowError(string title, string message, Exception? exception = null)
        {
            var detail = exception != null ? $"{message}\n{exception.Message}" : message;
            _radzenNotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = title,
                Detail = detail,
                Duration = 6000
            });
        }
    }
}