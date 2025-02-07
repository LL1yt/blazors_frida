using Grpc.Core;
using Google.Protobuf;
using BlazorFridaApp.MemoryScanner.Proto;

namespace BlazorFridaApp.Tests.Helpers;

public class MemoryScannerClientMock : MemoryScanner.MemoryScannerClient
{
    public override AsyncUnaryCall<ReadResponse> ReadMemoryAsync(
        ReadRequest request, 
        CallOptions options = default)
    {
        var response = new ReadResponse
        {
            Value = ByteString.CopyFrom(new byte[] { 1, 2, 3, 4 }),
            Success = true,
            ErrorMessage = string.Empty
        };

        return new AsyncUnaryCall<ReadResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<WriteResponse> WriteMemoryAsync(
        WriteRequest request,
        CallOptions options = default)
    {
        var response = new WriteResponse
        {
            Success = true,
            ErrorMessage = string.Empty
        };

        return new AsyncUnaryCall<WriteResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<ScanResponse> ScanMemoryAsync(
        ScanRequest request,
        CallOptions options = default)
    {
        var response = new ScanResponse
        {
            CheckpointId = "test-checkpoint"
        };
        response.Results.Add(new ScanResult 
        { 
            Address = 0x1000,
            Value = ByteString.CopyFrom(new byte[] { 1, 2, 3, 4 })
        });

        return new AsyncUnaryCall<ScanResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<StateResponse> GetStateAsync(
        StateRequest request,
        CallOptions options = default)
    {
        var response = new StateResponse
        {
            Version = "test-version"
        };
        response.State.Add("test-key", ByteString.CopyFrom(new byte[] { 1, 2, 3 }));

        return new AsyncUnaryCall<StateResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<SyncResponse> SyncStateAsync(
        SyncRequest request,
        CallOptions options = default)
    {
        var response = new SyncResponse
        {
            Success = true,
            ErrorMessage = string.Empty,
            NewVersion = "test-version-2"
        };

        return new AsyncUnaryCall<SyncResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }
}