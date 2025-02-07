using BlazorFridaApp.MemoryScanner.Services.Base;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Grpc.Core;

namespace BlazorFridaApp.MemoryScanner.Services;

public class FreezeGrpcService : BaseGrpcService
{
    private string _currentSessionId = string.Empty;

    public FreezeGrpcService(
        ILogger<FreezeGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
    }

    private async IAsyncEnumerable<(bool active, byte[] currentValue, string error)> StreamFreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync();
        var client = CreateClient(channel);
        var request = new Proto.FreezeRequest
        {
            SessionId = sessionId,
            Address = address,
            Value = Google.Protobuf.ByteString.CopyFrom(value),
            ValueType = valueType
        };

        using var call = client.FreezeValue(request, CreateMetadata(), cancellationToken: cancellationToken);
        var stream = call.ResponseStream;
        bool hasError = false;

        while (!hasError && !cancellationToken.IsCancellationRequested)
        {
            Proto.FreezeStatus? status = null;
            bool moveNextSuccess = false;

            try
            {
                moveNextSuccess = await stream.MoveNext(cancellationToken);
                if (moveNextSuccess)
                {
                    status = stream.Current;
                }
            }
            catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled)
            {
                _logger.LogError(ex, "Error during value freeze streaming for session {SessionId} at address {Address}", 
                    sessionId, address);
                hasError = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during value freeze streaming for session {SessionId} at address {Address}", 
                    sessionId, address);
                hasError = true;
            }

            if (!moveNextSuccess)
            {
                break;
            }

            if (status != null)
            {
                yield return (
                    status.Active,
                    status.CurrentValue.ToByteArray(),
                    status.ErrorMessage
                );
            }
        }
    }

    public IAsyncEnumerable<(bool active, byte[] currentValue, string error)> FreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        CancellationToken cancellationToken)
    {
        return StreamFreezeValueAsync(sessionId, address, value, valueType, cancellationToken);
    }

    public async Task UnfreezeValueAsync(string sessionId, ulong address)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var request = new Proto.UnfreezeRequest
            {
                SessionId = sessionId,
                Address = address
            };

            await client.UnfreezeValueAsync(request, CreateMetadata());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unfreeze value at {Address} for session {SessionId}", address, sessionId);
            throw;
        }
    }
}