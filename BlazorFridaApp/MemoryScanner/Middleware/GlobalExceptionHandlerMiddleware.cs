using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BlazorFridaApp.MemoryScanner.Exceptions;
using Microsoft.Extensions.Hosting;

namespace BlazorFridaApp.MemoryScanner.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            
            var response = context.Response;
            response.ContentType = "application/json";
            
            var (status, message) = ex switch
            {
                ProcessAccessException => (HttpStatusCode.Forbidden, "Access to the target process was denied"),
                MemoryScanException => (HttpStatusCode.InternalServerError, "Memory scan operation failed"),
                InvalidScanParametersException => (HttpStatusCode.BadRequest, "Invalid scan parameters provided"),
                MemoryWriteException => (HttpStatusCode.BadRequest, "Failed to write to process memory"),
                BlazorFridaException bfe => (HttpStatusCode.BadRequest, bfe.Message),
                InvalidOperationException => (HttpStatusCode.BadRequest, "Invalid operation performed"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Operation timed out"),
                UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied"),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
            };

            response.StatusCode = (int)status;
            await response.WriteAsJsonAsync(new 
            { 
                error = message, 
                errorCode = (ex as BlazorFridaException)?.ErrorCode ?? "BF500",
                details = ex.Message,
                stackTrace = _env.IsDevelopment() ? ex.StackTrace : null
            });
        }
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
