using Grpc.Core;
using Google.Protobuf;
using BlazorFridaApp.MemoryScanner.Proto;

namespace BlazorFridaApp.Tests.Helpers;

public class MemoryScannerClientErrorMock : BlazorFridaApp.MemoryScanner.Proto.MemoryScanner.MemoryScannerClient
{
    private static RpcException CreateError(string message) => 
        new(new Status(StatusCode.Internal, message));

    public override AsyncUnaryCall<ReadResponse> ReadMemoryAsync(
        ReadRequest request, 
        CallOptions options = default)
    {
        throw CreateError("Failed to read memory");
    }

    public override AsyncUnaryCall<WriteResponse> WriteMemoryAsync(
        WriteRequest request,
        CallOptions options = default)
    {
        throw CreateError("Failed to write memory");
    }

    public override AsyncUnaryCall<ScanResponse> ScanMemoryAsync(
        ScanRequest request,
        CallOptions options = default)
    {
        throw CreateError("Failed to scan memory");
    }

    public override AsyncUnaryCall<StateResponse> GetStateAsync(
        StateRequest request,
        CallOptions options = default)
    {
        throw CreateError("Failed to get state");
    }

    public override AsyncUnaryCall<SyncResponse> SyncStateAsync(
        SyncRequest request,
        CallOptions options = default)
    {
        throw CreateError("Failed to sync state");
    }
}