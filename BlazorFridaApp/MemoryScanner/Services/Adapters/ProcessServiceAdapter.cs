using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BlazorFridaApp.MemoryScanner.Services.Adapters;

public class ProcessServiceAdapter : IProcessService
{
    private readonly IMemoryScannerGrpcService _grpcService;

    public ProcessServiceAdapter(IMemoryScannerGrpcService grpcService)
    {
        _grpcService = grpcService;
    }

    public async Task<List<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var processes = await _grpcService.ListProcessesAsync();
        return processes.ToList();
    }

    public async Task<List<ProcessInfo>> RefreshProcessesAsync(CancellationToken cancellationToken = default)
    {
        return await GetProcessesAsync(cancellationToken);
    }

    public async Task<ProcessInfo> GetTargetProcessAsync(CancellationToken cancellationToken = default)
    {
        var processes = await GetProcessesAsync(cancellationToken);
        return processes.FirstOrDefault() ?? throw new InvalidOperationException("No accessible processes found");
    }
}