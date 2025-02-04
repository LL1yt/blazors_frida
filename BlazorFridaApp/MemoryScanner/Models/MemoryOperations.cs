using System;
using System.Collections.Generic;

namespace BlazorFridaApp.MemoryScanner.Models
{
    public record ReadRequest(
        int ProcessId,
        nint Address,
        int Size,
        string CorrelationId,
        int Version = 1
    );

    public record MemoryReadResult(
        byte[] Data,
        int Version
    );

    public record ScanRequest(
        int ProcessId,
        nint StartAddress,
        nint EndAddress,
        string Pattern,
        string? Mask,
        string CorrelationId,
        int Version = 1
    );

    public record MemoryScanResult(
        List<ulong> Addresses,
        int Version
    );
}