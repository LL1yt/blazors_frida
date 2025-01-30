namespace BlazorFridaApp.MemoryScanner.Models
{
    public class LockedAddress
    {
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public long Address { get; set; }
        public string ValueType { get; set; } = string.Empty;
        public byte[] OriginalBytes { get; set; } = System.Array.Empty<byte>();
        public byte[] CurrentValue { get; set; } = System.Array.Empty<byte>();
        public bool IsFrozen { get; set; }
        public DateTime LastAccessed { get; set; }

        // Navigation properties
        public int ProcessSettingsId { get; set; }
        public ProcessSettings ProcessSettings { get; set; } = null!;
    }
}