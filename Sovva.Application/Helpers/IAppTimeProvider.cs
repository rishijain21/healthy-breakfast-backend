namespace Sovva.Application.Helpers;

/// <summary>
/// Injected time provider for consistent timezone handling and testability
/// </summary>
public interface IAppTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly TodayIst { get; }
    DateOnly TomorrowIst { get; }
    DateTime ToUtc(DateTime istDateTime);
    DateTime ToIst(DateTime utcDateTime);
}
