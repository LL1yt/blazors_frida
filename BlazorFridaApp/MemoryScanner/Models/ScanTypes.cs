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

    public enum MemoryValueType
    {
        Byte,
        Short,
        Int,
        Long,
        Float,
        Double,
        String,
        Array
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
                MemoryValueType.Short => 2,
                MemoryValueType.Int => 4,
                MemoryValueType.Float => 4,
                MemoryValueType.Long => 8,
                MemoryValueType.Double => 8,
                _ => throw new ArgumentException($"Unsupported value type: {valueType}")
            };
        }

        public static bool IsNumeric(this MemoryValueType valueType)
        {
            return valueType switch
            {
                MemoryValueType.String => false,
                MemoryValueType.Array => false,
                _ => true
            };
        }
    }
}