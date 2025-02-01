using System.Diagnostics;
using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.MemoryScanner.Services.Interfaces;
using BlazorFridaApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Services
{
    public partial class ScanProfileService : IScanProfileService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ScanProfileService> _logger;

        public ScanProfileService(AppDbContext dbContext, ILogger<ScanProfileService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }
    }
}