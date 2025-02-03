using System;
using System.Threading.Tasks;

namespace BlazorFridaApp.Services.Interfaces
{
    public interface IMemoryCleanupService
    {
        Task CleanupAsync();
    }
} 