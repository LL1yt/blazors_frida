using BlazorFridaApp.MemoryScanner.Proto;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BlazorFridaApp.MemoryScanner.Services.Adapters
{
    public class MemoryReaderServiceAdapter : IMemoryReaderService
    {
        private readonly MemoryScannerService.MemoryScannerServiceClient _grpcClient;
        private readonly ILogger<MemoryReaderServiceAdapter> _logger;

        public MemoryReaderServiceAdapter(
            MemoryScannerService.MemoryScannerServiceClient grpcClient,
            ILogger<MemoryReaderServiceAdapter> logger)
        {
            _grpcClient = grpcClient;
            _logger = logger;
        }

        public async Task<MemoryReadResult> ReadMemoryAsync(ReadRequest request)
        {
            try
            {
                var response = await _grpcClient.ReadMemoryAsync(new ReadRequestProto
                {
                    ProcessId = request.ProcessId,
                    Address = request.Address.ToString("X16"),
                    Size = (uint)request.Size,
                    CorrelationId = request.CorrelationId,
                    Version = request.Version
                });

                return new MemoryReadResult(
                    data: response.Data.ToByteArray(),
                    version: response.Version);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "[gRPC Error] ReadMemory failed for {Address} (PID: {ProcessId})", 
                    request.Address, request.ProcessId);
                throw new MemoryOperationException("Failed to read memory via gRPC", ex);
            }
        }

        public async Task<MemoryScanResult> ScanMemoryAsync(ScanRequest request)
        {
            try
            {
                var response = await _grpcClient.ScanMemoryAsync(new ScanRequestProto
                {
                    ProcessId = request.ProcessId,
                    StartAddress = request.StartAddress.ToString("X16"),
                    EndAddress = request.EndAddress.ToString("X16"),
                    Pattern = request.Pattern,
                    Mask = request.Mask ?? string.Empty,
                    CorrelationId = request.CorrelationId,
                    Version = request.Version
                });

                return new MemoryScanResult(
                    addresses: response.Addresses.ConvertAll(a => ulong.Parse(a, System.Globalization.NumberStyles.HexNumber)),
                    version: response.Version);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "[gRPC Error] ScanMemory failed in range {Start}-{End} (PID: {ProcessId})",
                    request.StartAddress, request.EndAddress, request.ProcessId);
                throw new MemoryOperationException("Failed to scan memory via gRPC", ex);
            }
        }
    }

    public class MemoryOperationException : Exception
    {
        public MemoryOperationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}