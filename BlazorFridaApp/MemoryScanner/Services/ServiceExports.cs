using System.Runtime.CompilerServices;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Persistence;

[assembly: InternalsVisibleTo("BlazorFridaApp.Tests")]

namespace BlazorFridaApp.MemoryScanner.Services
{
    public static class ServiceConstants
    {
        public static class Memory
        {
            public const int DefaultBufferSize = 4096;
            public const int MaxBufferSize = 1024 * 1024; // 1MB
            public const int MinBufferSize = 16;
            public const int DefaultAlignment = 4;
        }

        public static class Process
        {
            public const int MaxProcessNameLength = 260; // MAX_PATH
            public const int MinProcessId = 4; // System
            public const int MaxProcessRetries = 3;
            public const int RetryDelayMs = 100;
        }

        public static class Database
        {
            public const int MaxBatchSize = 1000;
            public const int CommandTimeout = 30; // seconds
        }
    }

    public static class ServiceExtensions
    {
        public static async Task<T> WithRetry<T>(
            this Task<T> task,
            int maxRetries = ServiceConstants.Process.MaxProcessRetries,
            int delayMs = ServiceConstants.Process.RetryDelayMs)
        {
            var attempts = 0;
            while (true)
            {
                try
                {
                    return await task;
                }
                catch (Exception) when (++attempts < maxRetries)
                {
                    await Task.Delay(delayMs * attempts);
                }
            }
        }

        public static async Task WithRetry(
            this Task task,
            int maxRetries = ServiceConstants.Process.MaxProcessRetries,
            int delayMs = ServiceConstants.Process.RetryDelayMs)
        {
            var attempts = 0;
            while (true)
            {
                try
                {
                    await task;
                    return;
                }
                catch (Exception) when (++attempts < maxRetries)
                {
                    await Task.Delay(delayMs * attempts);
                }
            }
        }

        public static async Task<ApplicationSetting> GetSettingAsync(
            this AppDbContext dbContext,
            string key,
            string defaultValue = "")
        {
            var setting = await dbContext.ApplicationSettings
                .FirstOrDefaultAsync(a => a.Key == key);

            if (setting == null)
            {
                setting = new ApplicationSetting
                {
                    Key = key,
                    Value = defaultValue,
                    LastModified = DateTime.UtcNow
                };
                await dbContext.ApplicationSettings.AddAsync(setting);
                await dbContext.SaveChangesAsync();
            }

            return setting;
        }

        public static async Task<T> GetSettingAsync<T>(
            this AppDbContext dbContext,
            string key,
            T defaultValue)
        {
            var setting = await dbContext.GetSettingAsync(key, defaultValue?.ToString() ?? "");
            if (typeof(T).IsEnum)
            {
                return (T)Enum.Parse(typeof(T), setting.Value);
            }
            return (T)Convert.ChangeType(setting.Value, typeof(T));
        }

        public static async Task SaveSettingAsync<T>(
            this AppDbContext dbContext,
            string key,
            T value)
        {
            var setting = await dbContext.GetSettingAsync(key);
            setting.Value = value?.ToString() ?? "";
            setting.LastModified = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }
}