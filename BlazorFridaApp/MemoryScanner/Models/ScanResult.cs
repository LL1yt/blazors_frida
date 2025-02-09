using System;
using System.Collections.Generic;

namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ScanResult
    {
        public int Id { get; set; }
        public int ProcessId { get; set; }
        public List<nint> Addresses { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ValueType { get; set; } = string.Empty;
        public byte[] Value { get; set; } = Array.Empty<byte>();
        public string ComparisonType { get; set; } = string.Empty;
    }
}