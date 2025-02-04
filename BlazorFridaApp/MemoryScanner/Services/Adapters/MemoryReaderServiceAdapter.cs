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
        private readonly IMemoryScannerGrpcService _grpcService;
        private readonly ILogger<MemoryReaderServiceAdapter> _logger;
        private string _sessionId = string.Empty;
        private int _processId;

        public nint ProcessHandle => (nint)_processId;

        public MemoryReaderServiceAdapter(
            IMemoryScannerGrpcService grpcService,
            ILogger<MemoryReaderServiceAdapter> logger)
        {
            _grpcService = grpcService;
            _logger = logger;
        }

        public async void OpenProcess(int processId)
        {
            try
            {
                var response = await _grpcService.AttachToProcessAsync(processId);
                if (!response.success)
                {
                    throw new MemoryOperationException("Failed to attach to process", null);
                }
                _processId = processId;
                _sessionId = response.sessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[gRPC Error] Failed to attach to process {ProcessId}", processId);
                throw new MemoryOperationException("Failed to attach to process via gRPC", ex);
            }
        }

        public async Task<byte[]> ReadMemoryBytes(nint address, int length)
        {
            try
            {
                var response = await _grpcService.ReadMemoryAsync(_sessionId, (ulong)address, length, "bytes");
                if (!response.success)
                {
                    throw new MemoryOperationException($"Failed to read memory: {response.error}", null);
                }
                return response.value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[gRPC Error] ReadMemory failed for address {Address}", address);
                throw new MemoryOperationException("Failed to read memory via gRPC", ex);
            }
        }

        public async Task WriteMemoryBytes(nint address, byte[] value)
        {
            try
            {
                var response = await _grpcService.WriteMemoryAsync(_sessionId, (ulong)address, value, "bytes");
                if (!response.success)
                {
                    throw new MemoryOperationException($"Failed to write memory: {response.error}", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[gRPC Error] WriteMemory failed for address {Address}", address);
                throw new MemoryOperationException("Failed to write memory via gRPC", ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    await _grpcService.DetachFromProcessAsync(_sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[gRPC Error] Failed to detach from process {ProcessId}", _processId);
                }
            }
        }
    }

    public class MemoryOperationException : Exception
    {
        public MemoryOperationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}