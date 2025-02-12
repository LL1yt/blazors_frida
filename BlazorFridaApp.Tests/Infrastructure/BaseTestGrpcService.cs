using BlazorFridaApp.MemoryScanner.Services.Base;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Services;

namespace BlazorFridaApp.Tests.Infrastructure;

public abstract class BaseTestGrpcService : BaseGrpcService
{
    protected BaseTestGrpcService(
        ILogger logger,
        IPythonProcessManager processManager)
        : base(logger, processManager)
    {
    }
} 