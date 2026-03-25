namespace Sovva.Application.Helpers;

/// <summary>
/// Centralized timezone utilities for IST (Asia/Kolkata)
/// </summary>
public static class TimeZoneHelper
{
    public static readonly TimeZoneInfo IST = 
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    /// <summary>
    /// Get current time in IST
    /// </summary>
    public static DateTime NowIST() => 
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IST);

    /// <summary>
    /// Get today's date in IST
    /// </summary>
    public static DateOnly TodayIST() => 
        DateOnly.FromDateTime(NowIST());

    /// <summary>
    /// Convert UTC DateTime to IST
    /// </summary>
    public static DateTime ToIST(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), IST);

    /// <summary>
    /// Convert IST DateTime to UTC
    /// </summary>
    public static DateTime ToUtc(DateTime ist) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(ist, DateTimeKind.Unspecified), IST);
}
