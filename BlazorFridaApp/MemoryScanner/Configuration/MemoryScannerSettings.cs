using System.ComponentModel.DataAnnotations;

namespace BlazorFridaApp.MemoryScanner.Configuration;

public class MemoryScannerSettings
{
    [Required]
    [Range(1, int.MaxValue)]
    public int DefaultReadSize { get; set; } = 4096;

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxReadSize { get; set; } = 1024 * 1024; // 1MB

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxWriteSize { get; set; } = 1024 * 1024; // 1MB

    [Required]
    public string DefaultValueType { get; set; } = "int32";

    [Required]
    public string DefaultComparisonType { get; set; } = "exact";

    [Required]
    [Range(1000, int.MaxValue)]
    public int DefaultScanTimeout { get; set; } = 30000; // 30 seconds

    [Required]
    public MemoryRangeSettings DefaultMemoryRanges { get; set; } = new()
    {
        DefaultStart = 0x00010000, // Start of typical process memory
        DefaultEnd = 0x7FFFFFFF,   // End of 32-bit address space
        MaxRangeSize = 1024UL * 1024UL * 1024UL // 1GB
    };

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxConcurrentOperations { get; set; } = 4;

    [Required]
    [Range(1024 * 1024, int.MaxValue)]
    public int MaxMemoryUsagePerProcess { get; set; } = 1024 * 1024 * 512; // 512MB

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxResultsCacheSize { get; set; } = 100;

    [Required]
    [Range(1, int.MaxValue)]
    public int ScanBufferSize { get; set; } = 4096;

    [Required]
    [Range(1000, int.MaxValue)]
    public int MemoryMonitorInterval { get; set; } = 30000; // 30 seconds

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxProcessesToTrack { get; set; } = 10;
}

public class MemoryRangeSettings
{
    [Required]
    public ulong DefaultStart { get; set; }

    [Required]
    public ulong DefaultEnd { get; set; }
    
    [Required]
    [Range(1024 * 1024, ulong.MaxValue)]
    public ulong MaxRangeSize { get; set; } = 1024UL * 1024UL * 1024UL; // 1GB
}