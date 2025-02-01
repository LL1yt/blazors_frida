using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaMemoryScannerService : IMemoryScannerService
    {
         public async Task<IEnumerable<string>> ScanAsync(Process process, string searchPattern, int scanType, ScanProfile profile)
         {
             // Dummy implementation: returns an empty list.
             return await Task.FromResult(new List<string>());
         }

         public async Task<List<nint>> ScanForPattern(int processId, byte[] pattern, string mask)
         {
             // Dummy implementation: returns an empty list.
             return await Task.FromResult(new List<nint>());
         }

         public async Task<List<nint>> ScanForValue(int processId, int value, MemoryValueType valueType)
         {
             // Dummy implementation: returns an empty list.
             return await Task.FromResult(new List<nint>());
         }

         public async Task<List<nint>> GetAllAddresses(int processId, MemoryValueType valueType)
         {
             // Dummy implementation: returns an empty list.
             return await Task.FromResult(new List<nint>());
         }
    }
}