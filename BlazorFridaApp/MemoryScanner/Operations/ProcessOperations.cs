using BlazorFridaApp.MemoryScanner.Base;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Operations
{
    public class ProcessOperations : MemoryScannerBase
    {
        private readonly IProcessService _processService;
        private readonly IScanProfileService _profileService;

        public ProcessOperations(
            IProcessService processService,
            IScanProfileService profileService,
            ILogger<ProcessOperations> logger) : base(logger)
        {
            _processService = processService;
            _profileService = profileService;
        }

        public List<ProcessInfo> GetProcesses()
        {
            return ExecuteWithLogging(
                () => Task.FromResult(_processService.GetAccessibleProcesses().ToList()),
                "Getting accessible processes").Result;
        }

        public async Task SaveLastProcess(int processId)
        {
            await ExecuteWithLogging(
                () => _profileService.SaveLastProcess(processId),
                "Saving last process",
                processId);
        }

        public async Task<int?> GetLastProcessId()
        {
            return await ExecuteWithLogging(
                () => _profileService.GetLastProcessId(),
                "Getting last process ID");
        }
    }
}