using System.Collections.Generic;

namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ScannerConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MemoryValueType ValueType { get; set; }
        public string ComparisonType { get; set; } = string.Empty;
        public string ScanType { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, string> CustomSettings { get; set; } = new();
    }
} 