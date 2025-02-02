using Microsoft.AspNetCore.Components;
using Radzen;
using BlazorFridaApp.MemoryScanner.Base;

namespace BlazorFridaApp.MemoryScanner.Components
{
    public abstract class MemoryScannerComponentBase : ComponentBase
    {
        [Inject] protected ILogger<MemoryScannerComponentBase> Logger { get; set; } = null!;
        [Inject] protected INotificationService NotificationService { get; set; } = null!;
        [Inject] protected IProcessMemoryScanner Scanner { get; set; } = null!;

        protected virtual void OnInitializedBase()
        {
            Logger.LogInformation($"{GetType().Name} initialized");
        }

        protected override void OnInitialized()
        {
            OnInitializedBase();
            base.OnInitialized();
        }

        protected void ShowError(string title, string message, Exception? ex = null)
        {
            NotificationService.ShowError(title, message, ex);
            if (ex != null)
                Logger.LogError(ex, $"{title}: {message}");
            else
                Logger.LogError($"{title}: {message}");
        }

        protected void ShowWarning(string title, string message)
        {
            NotificationService.ShowWarning(title, message);
            Logger.LogWarning($"{title}: {message}");
        }

        protected void ShowInfo(string title, string message)
        {
            NotificationService.ShowInfo(title, message);
            Logger.LogInformation($"{title}: {message}");
        }

        protected void ShowSuccess(string title, string message)
        {
            NotificationService.ShowSuccess(title, message);
            Logger.LogInformation($"{title}: {message}");
        }
    }

    public static class ComponentConstants
    {
        public static class UI
        {
            public const string DefaultDateFormat = "yyyy-MM-dd HH:mm:ss";
            public const string DefaultNumberFormat = "0.##";
            public const string DefaultHexFormat = "X8";
            public const int DefaultPageSize = 10;
            public const string DefaultEmptyText = "No data available";
            public const string DefaultLoadingText = "Loading...";
            public const string DefaultErrorText = "An error occurred";
        }

        public static class CSS
        {
            public const string DefaultButtonClass = "rz-button-md";
            public const string PrimaryButtonClass = "rz-button-md rz-primary";
            public const string SecondaryButtonClass = "rz-button-md rz-secondary";
            public const string DangerButtonClass = "rz-button-md rz-danger";
            public const string SuccessButtonClass = "rz-button-md rz-success";
            public const string WarningButtonClass = "rz-button-md rz-warning";
            public const string InfoButtonClass = "rz-button-md rz-info";
        }

        public static class Events
        {
            public const int DebounceTime = 300; // ms
            public const int ThrottleTime = 100; // ms
            public const int AutoSaveDelay = 1000; // ms
            public const int RefreshInterval = 5000; // ms
        }
    }
}