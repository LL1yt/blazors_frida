using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public class FridaProcessService : IProcessService
    {
        public async Task<Process> GetTargetProcessAsync()
        {
            // For demonstration purposes, return the first accessible process.
            return await Task.FromResult(GetAccessibleProcesses().FirstOrDefault());
        }

        public IEnumerable<Process> GetAccessibleProcesses()
        {
            try
            {
                return Process.GetProcesses();
            }
            catch (Exception ex)
            {
                // Log the exception as needed.
                return new List<Process>();
            }
        }
    }
}
