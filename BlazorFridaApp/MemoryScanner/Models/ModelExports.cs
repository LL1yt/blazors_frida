using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BlazorFridaApp.Tests")]

namespace BlazorFridaApp.MemoryScanner.Models
{
    public static class ModelConstants
    {
        public static class Settings
        {
            public const string LastProcess = "LastProcess";
            public const string LastScanType = "LastScanType";
            public const string LastValueType = "LastValueType";
            public const string LastPattern = "LastPattern";
            public const string LastMask = "LastMask";
        }

        public static class Memory
        {
            public const int MinScanValue = -999999999;
            public const int MaxScanValue = 999999999;
            public const int MaxPatternLength = 256;
            public const int MaxMaskLength = 256;
            public const int DefaultRefreshRate = 100; // ms
            public const int MaxRefreshRate = 1000; // ms
            public const int MinRefreshRate = 10; // ms
        }

        public static class Database
        {
            public const int MaxProcessNameLength = 255;
            public const int MaxValueTypeLength = 50;
            public const int MaxSettingKeyLength = 50;
            public const int MaxSettingValueLength = 1000;
            public const int MaxNotesLength = 1000;
            public const int MaxNameLength = 255;
            public const int MaxMaskLength = 255;
        }
    }

    public static class ModelExtensions
    {
        public static bool IsValidScanValue(this int value)
        {
            return value >= ModelConstants.Memory.MinScanValue && 
                   value <= ModelConstants.Memory.MaxScanValue;
        }

        public static bool IsValidPattern(this byte[] pattern)
        {
            return pattern != null && 
                   pattern.Length > 0 && 
                   pattern.Length <= ModelConstants.Memory.MaxPatternLength;
        }

        public static bool IsValidMask(this string mask, byte[] pattern)
        {
            return !string.IsNullOrWhiteSpace(mask) && 
                   mask.Length == pattern.Length &&
                   mask.Length <= ModelConstants.Memory.MaxMaskLength &&
                   mask.All(c => c == 'x' || c == '?');
        }

        public static bool IsValidRefreshRate(this int rate)
        {
            return rate >= ModelConstants.Memory.MinRefreshRate && 
                   rate <= ModelConstants.Memory.MaxRefreshRate;
        }
    }
}