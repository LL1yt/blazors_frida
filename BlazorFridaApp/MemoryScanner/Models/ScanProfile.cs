namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ScanProfile
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public byte[] Pattern { get; set; } = System.Array.Empty<byte>();
        public string Mask { get; set; } = string.Empty;
        public int[] Offsets { get; set; } = System.Array.Empty<int>();
        public string ComparisonType { get; set; } = "exact";
        public DateTime Created { get; set; }
        public DateTime LastUsed { get; set; }

        // Navigation properties
        public int ProcessSettingsId { get; set; }
        public ProcessSettings ProcessSettings { get; set; } = null!;
    }
}