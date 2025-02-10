using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IProcessService
    {
        Task<List<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default);
        Task<List<ProcessInfo>> RefreshProcessesAsync(CancellationToken cancellationToken = default);
        Task<ProcessInfo> GetTargetProcessAsync(CancellationToken cancellationToken = default);
    }
}