using System.Diagnostics;
using Serilog.Context;

namespace Sovva.WebAPI.Middleware;

/// <summary>
/// Middleware that adds a correlation ID to each request for distributed tracing.
/// Uses X-Correlation-Id header if present, otherwise generates a new one.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Correlation-Id";
    private const int ShortIdLength = 8;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from incoming header, or generate a new one
        var correlationId = GetOrGenerateCorrelationId(context);

        // Add to response headers so client can see it
        context.Response.Headers[HeaderName] = correlationId;

        // Store in HttpContext.Items for downstream access
        context.Items[HeaderName] = correlationId;

        // Push to Serilog context so all logs include it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        // Check for incoming correlation ID
        var incomingId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(incomingId))
        {
            // Use incoming ID (but sanitize to ensure it's safe)
            return SanitizeCorrelationId(incomingId);
        }

        // Generate a new short 8-character ID (more readable than full GUID)
        return Guid.NewGuid().ToString("N")[..ShortIdLength];
    }

    private static string SanitizeCorrelationId(string id)
    {
        // Remove any characters that could cause issues in logs
        // Keep only alphanumeric characters and limit length
        var sanitized = new string(id.Where(c => char.IsLetterOrDigit(c)).ToArray());
        return sanitized.Length > ShortIdLength 
            ? sanitized[..ShortIdLength] 
            : sanitized;
    }
}