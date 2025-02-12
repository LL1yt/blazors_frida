namespace BlazorFridaApp.MemoryScanner.Models;

public class ScanConfiguration
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string ProcessName { get; set; }
    public required byte[] Pattern { get; set; }
    public required string Mask { get; set; }
    public required string ComparisonType { get; set; }
    public required string Type { get; set; }
}