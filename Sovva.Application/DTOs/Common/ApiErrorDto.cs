namespace Sovva.Application.DTOs;

/// <summary>
/// Standardized API error response with structured error codes
/// </summary>
public class ApiErrorDto
{
    /// <summary>
    /// Always false for error responses
    /// </summary>
    public bool Success { get; set; } = false;

    /// <summary>
    /// Machine-readable error code (e.g., "INSUFFICIENT_BALANCE")
    /// </summary>
    public string Code { get; set; } = "UNKNOWN_ERROR";

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional detailed error information for debugging
    /// </summary>
    public string? Detail { get; set; }
}