namespace BlazorFridaApp.MemoryScanner.Models
{
    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public static ProcessInfo FromProcess(System.Diagnostics.Process process)
        {
            return new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName
            };
        }
    }
}