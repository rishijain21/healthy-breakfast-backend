namespace Sovva.Domain.Enums
{
    /// <summary>
    /// Represents the lifecycle state of a user account.
    /// Replaces magic strings ("Active", "Deactivated", "Deleted") with type-safe enum values.
    /// Database column stores the string representation via EF Core value converter.
    /// </summary>
    public enum AccountStatus
    {
        Active = 0,
        Deactivated = 1,
        Deleted = 2
    }
}
