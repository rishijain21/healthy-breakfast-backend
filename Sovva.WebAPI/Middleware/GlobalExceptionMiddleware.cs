using Sovva.Domain.Constants;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using FluentValidation;

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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, code, message) = ex switch
        {
            // ✅ CUSTOM EXCEPTIONS
            InsufficientBalanceException ibEx => HandleInsufficientBalance(ibEx),
            AddressNotFoundException => 
                (StatusCodes.Status400BadRequest, "NO_DELIVERY_ADDRESS", "Please add a delivery address before placing an order."),
            DuplicateSubscriptionException => 
                (StatusCodes.Status409Conflict, "DUPLICATE_SUBSCRIPTION", "You already have an active subscription for this meal."),
            OrderNotFoundException onf =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", onf.Message),
            ScheduledOrderNotFoundException sonf =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", sonf.Message),
            UserNotFoundException unf =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", unf.Message),
            OrderAlreadyPreparedException oap =>
                (StatusCodes.Status409Conflict, "CONFLICT", oap.Message),

            // Not Found errors
            KeyNotFoundException => 
                (StatusCodes.Status404NotFound, ErrorCodes.NotFound, "The requested resource was not found."),

            // Validation errors
            ValidationException valEx => 
                (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", string.Join("; ", valEx.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))),

            // ✅ FIX 14: Use BusinessRuleException for safe client-facing messages
            Sovva.Domain.Exceptions.BusinessRuleException bre => 
                (StatusCodes.Status400BadRequest, "BUSINESS_RULE_ERROR", bre.Message),

            // ✅ FIX 14: Hide internal InvalidOperationException messages from clients
            InvalidOperationException => 
                (StatusCodes.Status400BadRequest, ErrorCodes.InvalidOperation, "An error occurred while processing your request."),

            // Unauthorized - access denied
            UnauthorizedAccessException => 
                (StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Forbidden"),

            // Argument errors
            ArgumentException => 
                (StatusCodes.Status400BadRequest, ErrorCodes.InvalidArgument, "Invalid request data."),

            // Database errors
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "This record was modified by another request. Please retry."),

            Microsoft.EntityFrameworkCore.DbUpdateException =>
                (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "A database error occurred."),
                
            Npgsql.PostgresException =>
                (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "A database error occurred."),

            // Default - internal server error
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
        }
        else if (ex is InvalidOperationException)
        {
            // ✅ FIX 14: Log internal IOE message server-side with correlation id
            _logger.LogError(ex, "InvalidOperationException [{CorrelationId}] for {Path}: {Message}", 
                context.TraceIdentifier, context.Request.Path, ex.Message);
        }
        else
        {
            _logger.LogWarning("Handled exception for {Path}: {Code} - {Message}", 
                context.Request.Path, code, message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            success = false,
            code = code,
            message = message
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private (int statusCode, string code, string message) HandleInsufficientBalance(InsufficientBalanceException ex)
    {
        _logger.LogWarning("Insufficient balance: Required={Required}, Available={Available}", 
            ex.Required, ex.Available);
            
        return (StatusCodes.Status400BadRequest, "INSUFFICIENT_BALANCE", 
            "You don't have enough balance to complete this order.");
    }
}
