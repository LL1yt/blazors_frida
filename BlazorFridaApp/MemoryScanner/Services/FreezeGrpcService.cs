using BlazorFridaApp.MemoryScanner.Services.Base;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Google.Protobuf;
using System.Collections.Generic;

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
        var results = new List<(bool active, byte[] currentValue, string error)>();
        var channel = await GetChannelAsync();
        var client = CreateClient(channel);
        var request = new Proto.FreezeRequest
        {
            SessionId = sessionId,
            Address = address,
            Value = ByteString.CopyFrom(value),
            ValueType = valueType
        };

        try
        {
            using var freezeStream = client.FreezeValue(request, CreateMetadata(), cancellationToken: cancellationToken);
            await foreach (var response in freezeStream.ResponseStream.ReadAllAsync(cancellationToken))
            {
                results.Add((
                    response.Active,
                    response.CurrentValue.ToByteArray(),
                    response.ErrorMessage
                ));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during freeze operation for address {Address:X}", address);
            results.Add((false, System.Array.Empty<byte>(), ex.Message));
        }

        foreach (var result in results)
        {
            yield return result;
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