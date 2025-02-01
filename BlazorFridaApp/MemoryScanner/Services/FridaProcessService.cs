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
            var target = GetAccessibleProcesses().FirstOrDefault();
            if (target == null)
            {
                throw new InvalidOperationException("No accessible process found.");
            }
            return await Task.FromResult(target);
        }

        public IEnumerable<Process> GetAccessibleProcesses()
        {
            try
            {
                return Process.GetProcesses();
            }
            catch (Exception)
            {
                // Log the exception as needed.
                return new List<Process>();
            }
        }
    }
}
