using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.MemoryScanner.Services.Adapters;

public class ProcessServiceAdapter : IProcessService
{
    private readonly IMemoryScannerGrpcService _grpcService;

    public ProcessServiceAdapter(IMemoryScannerGrpcService grpcService)
    {
        _grpcService = grpcService;
    }

    public async Task<List<ProcessInfo>> GetAccessibleProcessesAsync()
    {
        var processes = await _grpcService.ListProcessesAsync();
        return processes.ToList();
    }

    public async Task<ProcessInfo> GetTargetProcessAsync()
    {
        var processes = await GetAccessibleProcessesAsync();
        return processes.First();
    }
}