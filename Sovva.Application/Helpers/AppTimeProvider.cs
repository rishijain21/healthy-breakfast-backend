namespace Sovva.Application.Helpers;

/// <summary>
/// Production implementation of IAppTimeProvider using system time
/// </summary>
public sealed class AppTimeProvider : IAppTimeProvider
{
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly TodayIst => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist));

    public DateOnly TomorrowIst => TodayIst.AddDays(1);

    public DateTime ToUtc(DateTime istDateTime) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(istDateTime, DateTimeKind.Unspecified), Ist);

    public DateTime ToIst(DateTime utcDateTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), Ist);
}
