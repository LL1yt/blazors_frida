namespace BlazorFridaApp.MemoryScanner.Models
{
    public enum ScanType
    {
        ExactValue,
        UnknownInitialValue,
        IncreasedValue,
        DecreasedValue,
        ChangedValue,
        UnchangedValue,
        Pattern
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
}