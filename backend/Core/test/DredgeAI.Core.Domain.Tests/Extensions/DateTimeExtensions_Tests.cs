using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.Extensions;

public class DateTimeExtensions_Tests : DredgeAICoreDomainTestBase
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── GetTimestamp ──────────────────────────────────────────────────

    [Fact]
    public void GetTimestamp_Epoch_ReturnsZero()
    {
        Epoch.GetTimestamp().ShouldBe(0);
    }

    [Fact]
    public void GetTimestamp_UtcTime_ReturnsCorrectSeconds()
    {
        var dt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var expected = (long)(dt - Epoch).TotalSeconds;
        dt.GetTimestamp().ShouldBe(expected);
    }

    [Fact]
    public void GetTimestamp_NonUtc_Throws()
    {
        var local = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Should.Throw<AbpException>(() => local.GetTimestamp());
    }

    [Fact]
    public void GetTimestamp_UnspecifiedKind_Throws()
    {
        var unspecified = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Should.Throw<AbpException>(() => unspecified.GetTimestamp());
    }

    // ── GetTimestampByMilliseconds ────────────────────────────────────

    [Fact]
    public void GetTimestampByMilliseconds_Epoch_ReturnsZero()
    {
        Epoch.GetTimestampByMilliseconds().ShouldBe(0);
    }

    [Fact]
    public void GetTimestampByMilliseconds_UtcTime_ReturnsCorrectMs()
    {
        var dt = new DateTime(2025, 6, 15, 10, 30, 0, 123, DateTimeKind.Utc);
        var expected = (long)(dt - Epoch).TotalMilliseconds;
        dt.GetTimestampByMilliseconds().ShouldBe(expected);
    }

    [Fact]
    public void GetTimestampByMilliseconds_NonUtc_Throws()
    {
        var local = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Should.Throw<AbpException>(() => local.GetTimestampByMilliseconds());
    }

    // ── ToDateTime (seconds) ──────────────────────────────────────────

    [Fact]
    public void ToDateTime_Zero_ReturnsEpoch()
    {
        0L.ToDateTime().ShouldBe(Epoch);
    }

    [Fact]
    public void ToDateTime_KnownTimestamp_ReturnsCorrectUtc()
    {
        // 2025-06-15T10:30:00Z
        long ts = 1749983400;
        ts.ToDateTime().ShouldBe(new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToDateTime_NegativeTimestamp_ReturnsPreEpoch()
    {
        (-1L).ToDateTime().ShouldBe(new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    // ── ToDateTimeByMilliseconds ──────────────────────────────────────

    [Fact]
    public void ToDateTimeByMilliseconds_Zero_ReturnsEpoch()
    {
        0L.ToDateTimeByMilliseconds().ShouldBe(Epoch);
    }

    [Fact]
    public void ToDateTimeByMilliseconds_KnownMsTimestamp_ReturnsCorrectUtc()
    {
        // 2025-06-15T10:30:00.123Z
        long ms = 1749983400123;
        ms.ToDateTimeByMilliseconds()
            .ShouldBe(new DateTime(2025, 6, 15, 10, 30, 0, 123, DateTimeKind.Utc));
    }

    [Fact]
    public void ToDateTimeByMilliseconds_Negative_ReturnsPreEpoch()
    {
        (-1L).ToDateTimeByMilliseconds()
            .ShouldBe(new DateTime(1969, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    // ── ToDisplayText (TimeSpan) ──────────────────────────────────────

    [Fact]
    public void ToDisplayText_Days_ShowsDays()
    {
        var ts = new TimeSpan(2, 5, 8, 3); // 2 days, 5h, 8m, 3s
        ts.ToDisplayText().ShouldBe("2天 05时08分03秒");
    }

    [Fact]
    public void ToDisplayText_HoursOnly_ShowsHours()
    {
        var ts = new TimeSpan(3, 45, 7); // 3h, 45m, 7s
        ts.ToDisplayText().ShouldBe("03时45分07秒");
    }

    [Fact]
    public void ToDisplayText_MinutesOnly_ShowsMinutes()
    {
        var ts = new TimeSpan(0, 7, 5); // 7m, 5s
        ts.ToDisplayText().ShouldBe("07分05秒");
    }

    [Fact]
    public void ToDisplayText_SecondsOnly_ShowsSeconds()
    {
        var ts = new TimeSpan(0, 0, 42);
        ts.ToDisplayText().ShouldBe("42秒");
    }

    [Fact]
    public void ToDisplayText_Zero_ShowsZeroSeconds()
    {
        var ts = TimeSpan.Zero;
        ts.ToDisplayText().ShouldBe("00秒");
    }

    // ── ChinaTimeToUtc ────────────────────────────────────────────────

    [Theory]
    [InlineData(2025, 7, 19, 14, 30, 0)]   // Beijing afternoon
    [InlineData(2025, 1, 1, 0, 0, 0)]      // Beijing midnight
    [InlineData(2025, 6, 15, 8, 0, 0)]     // Beijing 8am = UTC 0
    public void ChinaTimeToUtc_ConvertsCorrectly(int y, int M, int d, int h, int m, int s)
    {
        var chinaWallClock = new DateTime(y, M, d, h, m, s); // Kind=Unspecified
        var utc = chinaWallClock.ChinaTimeToUtc();

        var expectedUtc = new DateTimeOffset(y, M, d, h, m, s, TimeSpan.FromHours(8))
            .ToUniversalTime();
        utc.ShouldBe(expectedUtc);
        utc.Offset.ShouldBe(TimeSpan.Zero);
    }
}
