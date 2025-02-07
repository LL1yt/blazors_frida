using System.ComponentModel.DataAnnotations;

namespace BlazorFridaApp.MemoryScanner.Configuration;

public class MemoryScannerSettings
{
    [Required]
    [Range(1, int.MaxValue)]
    public int DefaultReadSize { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxReadSize { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxWriteSize { get; set; }

    [Required]
    public string DefaultValueType { get; set; } = "int32";

    [Required]
    public string DefaultComparisonType { get; set; } = "exact";

    [Required]
    [Range(1000, int.MaxValue)]
    public int DefaultScanTimeout { get; set; }

    [Required]
    public MemoryRangeSettings DefaultMemoryRanges { get; set; } = new();
}

public class MemoryRangeSettings
{
    [Required]
    public ulong DefaultStart { get; set; }

    [Required]
    public ulong DefaultEnd { get; set; }
} 