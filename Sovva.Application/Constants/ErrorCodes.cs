namespace Sovva.Application.Constants;

/// <summary>
/// Standardized error codes for API responses
/// </summary>
public static class ErrorCodes
{
    // Wallet errors
    public const string InsufficientBalance = "INSUFFICIENT_BALANCE";
    public const string WalletNotFound = "WALLET_NOT_FOUND";

    // Order errors
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderAlreadyProcessed = "ORDER_ALREADY_PROCESSED";
    public const string OrderCannotModify = "ORDER_CANNOT_MODIFY";

    // Scheduled Order errors
    public const string ScheduledOrderNotFound = "SCHEDULED_ORDER_NOT_FOUND";
    public const string ScheduledOrderExpired = "SCHEDULED_ORDER_EXPIRED";

    // Subscription errors
    public const string SubscriptionNotFound = "SUBSCRIPTION_NOT_FOUND";
    public const string DuplicateSubscription = "DUPLICATE_SUBSCRIPTION";
    public const string SubscriptionExpired = "SUBSCRIPTION_EXPIRED";
    public const string SubscriptionInactive = "SUBSCRIPTION_INACTIVE";

    // Address errors
    public const string NoDeliveryAddress = "NO_DELIVERY_ADDRESS";
    public const string LocationNotServiceable = "LOCATION_NOT_SERVICEABLE";
    public const string AddressNotFound = "ADDRESS_NOT_FOUND";

    // Authentication/Authorization errors
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";

    // General errors
    public const string InvalidOperation = "INVALID_OPERATION";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";
}