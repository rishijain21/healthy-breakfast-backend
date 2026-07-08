using Sovva.Application.Helpers;

namespace Sovva.IntegrationTests.Helpers;

/// <summary>
/// Deterministic IAppTimeProvider for integration tests.
/// Returns a fixed UTC timestamp so test assertions are stable.
/// </summary>
public class TestTimeProvider : IAppTimeProvider
{
    private readonly DateTime _fixedUtc;

    public TestTimeProvider(DateTime? fixedUtc = null)
    {
        _fixedUtc = fixedUtc ?? new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow => _fixedUtc;

    public DateOnly TodayIst => DateOnly.FromDateTime(ToIst(_fixedUtc));

    public DateOnly TomorrowIst => TodayIst.AddDays(1);

    public DateTime NowIst => ToIst(_fixedUtc);

    public DateTime ToIst(DateTime utc) => utc.AddHours(5).AddMinutes(30);

    public DateTime ToUtc(DateTime ist) => ist.AddHours(-5).AddMinutes(-30);
}
