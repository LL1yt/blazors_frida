using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IProcessService
    {
        Task<ProcessInfo> GetTargetProcessAsync();
        IEnumerable<ProcessInfo> GetAccessibleProcesses();
    }
}