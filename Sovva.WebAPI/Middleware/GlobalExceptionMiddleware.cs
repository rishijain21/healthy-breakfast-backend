using Sovva.Application.Constants;
using Sovva.Application.DTOs;

namespace Sovva.WebAPI.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ✅ Bypass for Hangfire — it manages its own auth/response pipeline
        if (context.Request.Path.StartsWithSegments("/hangfire"))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", 
                context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, code, message) = ex switch
        {
            // Not Found errors
            KeyNotFoundException => 
                (StatusCodes.Status404NotFound, ErrorCodes.NotFound, ex.Message),

            // Invalid Operation errors - categorize by message content
            InvalidOperationException operationEx when operationEx.Message.Contains("wallet", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, ErrorCodes.InsufficientBalance, operationEx.Message),

            InvalidOperationException operationEx when operationEx.Message.Contains("address", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, ErrorCodes.NoDeliveryAddress, operationEx.Message),

            InvalidOperationException operationEx when operationEx.Message.Contains("subscription", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, ErrorCodes.SubscriptionNotFound, operationEx.Message),

            InvalidOperationException operationEx when operationEx.Message.Contains("order", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status400BadRequest, ErrorCodes.InvalidOperation, operationEx.Message),

            InvalidOperationException => 
                (StatusCodes.Status400BadRequest, ErrorCodes.InvalidOperation, ex.Message),

            // Unauthorized - access denied
            UnauthorizedAccessException => 
                (StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Forbidden"),

            // Argument errors
            ArgumentException => 
                (StatusCodes.Status400BadRequest, ErrorCodes.InvalidArgument, ex.Message),

            // Default - internal server error
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ApiErrorDto
        {
            Success = false,
            Code = code,
            Message = message,
#if DEBUG
            Detail = ex.Message // Only show details in debug mode
#endif
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}
