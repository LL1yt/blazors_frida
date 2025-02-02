using System;
using System.Collections.Generic;

namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ScanResult
    {
        public int ProcessId { get; set; }
        public byte[] Pattern { get; set; } = System.Array.Empty<byte>();
        public string Mask { get; set; } = string.Empty;
        public List<nint> Addresses { get; set; } = new();
    }
}