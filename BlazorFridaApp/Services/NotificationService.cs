using System;
using Radzen;

namespace BlazorFridaApp.Services
{
    public class NotificationService : INotificationService
    {
        private readonly Radzen.NotificationService _radzenNotificationService;

        public NotificationService(Radzen.NotificationService radzenNotificationService)
        {
            _radzenNotificationService = radzenNotificationService ?? throw new ArgumentNullException(nameof(radzenNotificationService));
        }

        public void ShowInfo(string title, string message)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

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
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

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
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            var detail = exception != null ? $"{message}\n{exception.Message}" : message;
            _radzenNotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = title,
                Detail = detail,
                Duration = 6000
            });
        }

        public void ShowSuccess(string title, string message)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            _radzenNotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = title,
                Detail = message,
                Duration = 4000
            });
        }
    }
}