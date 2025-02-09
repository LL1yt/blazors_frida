namespace BlazorFridaApp.MemoryScanner.Models
{
    public enum ScanType
    {
        ExactValue,
        Pattern,
        IncreasedValue,
        DecreasedValue,
        ChangedValue,
        UnchangedValue,
        UnknownInitialValue
    }

    public static class ScanTypeExtensions
    {
        public static bool RequiresInitialScan(this ScanType scanType)
        {
            return scanType switch
            {
                ScanType.IncreasedValue => true,
                ScanType.DecreasedValue => true,
                ScanType.ChangedValue => true,
                ScanType.UnchangedValue => true,
                ScanType.UnknownInitialValue => true,
                _ => false
            };
        }

        public static bool RequiresPattern(this ScanType scanType)
        {
            return scanType == ScanType.Pattern;
        }

        public static bool RequiresValue(this ScanType scanType)
        {
            return scanType == ScanType.ExactValue;
        }
    }

    public static class MemoryValueTypeExtensions
    {
        public static int GetSize(this MemoryValueType valueType)
        {
            return valueType switch
            {
                MemoryValueType.Byte => 1,
                MemoryValueType.Int16 => 2,
                MemoryValueType.Int32 => 4,
                MemoryValueType.Float => 4,
                MemoryValueType.Int64 => 8,
                MemoryValueType.Double => 8,
                _ => throw new ArgumentException($"Unsupported value type: {valueType}")
            };
        }

        public static bool IsNumeric(this MemoryValueType valueType)
        {
            return valueType switch
            {
                MemoryValueType.String => false,
                MemoryValueType.ByteArray => false,
                _ => true
            };
        }
    }
}