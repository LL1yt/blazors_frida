using System.Diagnostics;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IProcessService : IMemoryScannerBaseService
    {
        List<Process> GetAccessibleProcesses();
    }
}