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