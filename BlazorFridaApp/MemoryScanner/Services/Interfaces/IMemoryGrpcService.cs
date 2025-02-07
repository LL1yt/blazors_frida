using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces;

public interface IMemoryGrpcService
{
    void OpenProcess(int processId);
    Task<byte[]> ReadMemoryBytes(nint address, int length);
    Task WriteMemoryBytes(nint address, byte[] value);
    Task<(byte[] value, bool success, string error)> ReadMemoryBytes(string sessionId, ulong address, int size);
    Task<(bool success, string error)> WriteMemoryBytes(string sessionId, ulong address, byte[] value);
    nint ProcessHandle { get; }
}