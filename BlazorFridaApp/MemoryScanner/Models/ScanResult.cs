using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ScanResult
    {
        public int Id { get; set; }
        public int ProcessId { get; set; }
        
        [NotMapped]
        public List<nint> Addresses { get; set; } = new();

        public string AddressesJson
        {
            get => JsonSerializer.Serialize(Addresses.ConvertAll(addr => addr.ToInt64()));
            set => Addresses = JsonSerializer.Deserialize<List<long>>(value)?.ConvertAll(addr => new nint(addr)) ?? new();
        }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ValueType { get; set; } = string.Empty;
        public byte[] Value { get; set; } = Array.Empty<byte>();
        public string ComparisonType { get; set; } = string.Empty;
        public byte[]? Pattern { get; set; }
        public string? Mask { get; set; }
    }
}