using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Google.Protobuf;

namespace BlazorFridaApp.MemoryScanner.Services;

public class FreezeGrpcService : BaseGrpcService, IFreezeGrpcService
{
    private string _currentSessionId = string.Empty;

    public FreezeGrpcService(
        ILogger<FreezeGrpcService> logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
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

    private async IAsyncEnumerable<(bool active, byte[] currentValue, string error)> StreamFreezeValueAsync(
        string sessionId,
        ulong address,
        byte[] value,
        string valueType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            var channel = await GetChannelAsync();
            var client = CreateClient(channel);

            var request = new Proto.FreezeRequest
            {
                SessionId = sessionId,
                Address = address,
                Value = ByteString.CopyFrom(value),
                ValueType = valueType
            };

            using var freezeStream = client.FreezeValue(request, CreateMetadata(), cancellationToken: cancellationToken);
            await foreach (var response in freezeStream.ResponseStream.ReadAllAsync(cancellationToken))
            {
                yield return (
                    response.Active,
                    response.CurrentValue.ToByteArray(),
                    response.ErrorMessage
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during freeze operation for address {Address:X}", address);
            yield return (false, Array.Empty<byte>(), ex.Message);
        }
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
            _logger.LogError(ex, "Failed to unfreeze value at address {Address:X}", address);
            throw;
        }
    }
}