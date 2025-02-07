using BlazorFridaApp.MemoryScanner.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces;

public interface IProcessGrpcService
{
    Task<List<ProcessInfo>> GetAccessibleProcessesAsync();
    Task<ProcessInfo> GetTargetProcessAsync();
    Task<(bool success, string sessionId)> AttachToProcessAsync(int pid);
    Task DetachFromProcessAsync(string sessionId);
    nint ProcessHandle { get; }
}