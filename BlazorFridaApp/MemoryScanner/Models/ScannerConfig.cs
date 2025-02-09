using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

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

        [NotMapped]
        public Dictionary<string, string> CustomSettings { get; set; } = new();

        public string CustomSettingsJson
        {
            get => CustomSettings != null ? JsonSerializer.Serialize(CustomSettings) : "{}";
            set => CustomSettings = !string.IsNullOrEmpty(value) 
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new() 
                : new();
        }
    }
} 