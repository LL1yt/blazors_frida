using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BlazorFridaApp.MemoryScanner.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
                InvalidOperationException => (HttpStatusCode.BadRequest, "Invalid operation performed"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Operation timed out"),
                UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied"),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
            };

            response.StatusCode = (int)status;
            await response.WriteAsJsonAsync(new { error = message, details = ex.Message });
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
