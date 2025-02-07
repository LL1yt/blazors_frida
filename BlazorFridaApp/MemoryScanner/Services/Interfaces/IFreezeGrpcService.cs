using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Interfaces;

public interface IFreezeGrpcService
{
    IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken);
        
    Task UnfreezeValueAsync(string sessionId, ulong address);
}