namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ScanProfile
    {
        public int Id { get; set; }
        public string Name { get; set; } = "New Profile";
        public string ProcessName { get; set; } = string.Empty;
        public byte[] Pattern { get; set; } = Array.Empty<byte>();
        public string Mask { get; set; } = string.Empty;
        public int[] Offsets { get; set; } = Array.Empty<int>();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    }
}