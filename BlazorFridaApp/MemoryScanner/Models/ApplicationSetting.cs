namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ApplicationSetting
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }

        // Helper methods for common settings
        public static string LastProcessKey => "LastProcess";
        public static string LastScanTypeKey => "LastScanType";
        public static string LastValueTypeKey => "LastValueType";
    }
}