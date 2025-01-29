# ADR 001: Blazor Server Architecture with SQLite Persistence

## Status

Accepted (2025-01-29)

## Context

Require desktop-style memory analysis tool with:

- Real-time process scanning
- Cross-session state persistence
- Secure memory operations
- .NET 9 compatibility

## Decision

1. **Runtime**: Blazor Server over WebAssembly

   - Direct native interop without WASM constraints
   - Stateful session management
   - Desktop application feel

2. **Persistence**: SQLite with EF Core

   - Local database file storage
   - Schema migrations for versioned profiles
   - Encrypted sensitive data (scan configurations)

3. **Memory Safety**:

   ```csharp
   public unsafe class MemoryOperator
   {
       [UnmanagedCallersOnly]
       public static void SafeWrite(nint address, byte[] pattern)
       {
           // Hardware-assisted write validation
           if(!VirtualMemoryValidator.IsWritable(address))
               throw new AccessViolationException();

           fixed (byte* p = pattern)
           {
               Buffer.MemoryCopy(p, (void*)address, pattern.Length, pattern.Length);
           }
       }
   }
   ```

4. **Python Integration**:
   ```mermaid
   sequenceDiagram
       BlazorServer->>PythonEngine: IPC Request (JSON)
       PythonEngine->>Frida: Spawn Process
       Frida-->>PythonEngine: Memory Map
       PythonEngine-->>BlazorServer: Scan Results
   ```

## Compliance

- Avoid WebAssembly security constraints
- Meet desktop performance requirements
- Maintain session state during scans
- Enable safe low-level memory operations
