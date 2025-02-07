using Grpc.Core;
using Google.Protobuf;

namespace BlazorFridaApp.Tests.Helpers;

public class MemoryScannerClientMock : BlazorFridaApp.MemoryScanner.Proto.MemoryScanner.MemoryScannerClient
{
    public override AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.ReadResponse> ReadMemoryAsync(
        BlazorFridaApp.MemoryScanner.Proto.ReadRequest request, 
        CallOptions options = default)
    {
        var response = new BlazorFridaApp.MemoryScanner.Proto.ReadResponse
        {
            Value = ByteString.CopyFrom(new byte[] { 1, 2, 3, 4 }),
            Success = true,
            ErrorMessage = string.Empty
        };

        return new AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.ReadResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.WriteResponse> WriteMemoryAsync(
        BlazorFridaApp.MemoryScanner.Proto.WriteRequest request,
        CallOptions options = default)
    {
        var response = new BlazorFridaApp.MemoryScanner.Proto.WriteResponse
        {
            Success = true,
            ErrorMessage = string.Empty
        };

        return new AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.WriteResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.ScanResponse> ScanMemoryAsync(
        BlazorFridaApp.MemoryScanner.Proto.ScanRequest request,
        CallOptions options = default)
    {
        var response = new BlazorFridaApp.MemoryScanner.Proto.ScanResponse
        {
            CheckpointId = "test-checkpoint"
        };
        response.Results.Add(new BlazorFridaApp.MemoryScanner.Proto.ScanResult 
        { 
            Address = 0x1000,
            Value = ByteString.CopyFrom(new byte[] { 1, 2, 3, 4 })
        });

        return new AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.ScanResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.StateResponse> GetStateAsync(
        BlazorFridaApp.MemoryScanner.Proto.StateRequest request,
        CallOptions options = default)
    {
        var response = new BlazorFridaApp.MemoryScanner.Proto.StateResponse
        {
            Version = "test-version"
        };
        response.State.Add("test-key", ByteString.CopyFrom(new byte[] { 1, 2, 3 }));

        return new AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.StateResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.SyncResponse> SyncStateAsync(
        BlazorFridaApp.MemoryScanner.Proto.SyncRequest request,
        CallOptions options = default)
    {
        var response = new BlazorFridaApp.MemoryScanner.Proto.SyncResponse
        {
            Success = true,
            ErrorMessage = string.Empty,
            NewVersion = "test-version-2"
        };

        return new AsyncUnaryCall<BlazorFridaApp.MemoryScanner.Proto.SyncResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }
}