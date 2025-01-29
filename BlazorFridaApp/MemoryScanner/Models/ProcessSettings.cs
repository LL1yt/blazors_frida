namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ProcessSettings
    {
        public int Id { get; set; }
        public int LastProcessId { get; set; }
        public string LastProcessName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}