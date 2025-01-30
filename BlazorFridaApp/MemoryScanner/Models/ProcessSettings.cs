namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ProcessSettings
    {
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime LastAccessed { get; set; }
        public List<LockedAddress> LockedAddresses { get; set; } = new();
        public List<ScanProfile> ScanProfiles { get; set; } = new();
    }
}