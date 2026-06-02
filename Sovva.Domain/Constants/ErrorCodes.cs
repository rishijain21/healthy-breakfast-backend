namespace Sovva.Domain.Constants;

/// <summary>
/// Standardized error codes for API responses
/// </summary>
public static class ErrorCodes
{
    // Wallet errors
    public const string InsufficientBalance = "INSUFFICIENT_BALANCE";

    // Subscription errors
    public const string SubscriptionNotFound = "SUBSCRIPTION_NOT_FOUND";

    // Address errors
    public const string NoDeliveryAddress = "NO_DELIVERY_ADDRESS";
    public const string AddressNotFound = "ADDRESS_NOT_FOUND";

    // Authentication/Authorization errors
    public const string Forbidden = "FORBIDDEN";

    // General errors
    public const string InvalidOperation = "INVALID_OPERATION";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";
}