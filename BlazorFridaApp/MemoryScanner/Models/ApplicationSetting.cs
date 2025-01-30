namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ApplicationSetting
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}