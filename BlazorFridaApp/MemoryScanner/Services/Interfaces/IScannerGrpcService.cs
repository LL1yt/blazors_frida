using BlazorFridaApp.MemoryScanner.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces;

public interface IScannerGrpcService
{
    Task<IEnumerable<string>> ScanAsync(ProcessInfo process, string searchPattern, int scanType, ScanProfile profile);
    Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask);
    Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType);
    Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType);
    Task<IEnumerable<ScanResult>> ScanAsync(string sessionId, string valueType, byte[] value, string comparisonType, IEnumerable<(ulong start, ulong end)> ranges);
}