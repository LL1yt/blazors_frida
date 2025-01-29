namespace BlazorFridaApp.MemoryScanner.Models
{
    public class LockedAddress
    {
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public long Address { get; set; }
        public string ValueType { get; set; } = "int";
        public byte[] OriginalBytes { get; set; } = Array.Empty<byte>();
        public byte[] CurrentValue { get; set; } = Array.Empty<byte>();
        public bool IsFrozen { get; set; }
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
    }
}