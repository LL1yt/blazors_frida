using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IProcessService
    {
        Task<Process> GetTargetProcessAsync();
        IEnumerable<Process> GetAccessibleProcesses();
    }
}