using Trace.Presentation.Controls;

namespace Trace.Tests;

/// <summary>
/// How times read on screen, and that they come from the app's clock rather than the
/// machine's, so the tests and the seeded round agree with what is shown.
/// </summary>
[SetCulture("en-GB")]
public class DatesAndClockTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);

    [Test]
    public void Within_a_week_a_time_reads_relative()
    {
        RelativeTimeConverter.Format(Now.AddSeconds(-20), Now).Should().Be("Just now");
        RelativeTimeConverter.Format(Now.AddMinutes(-12), Now).Should().Be("12 min ago");
        RelativeTimeConverter.Format(Now.AddHours(-3), Now).Should().Be("3 h ago");
        RelativeTimeConverter.Format(Now.AddDays(-2), Now).Should().Be("2 d ago");
    }

    [Test]
    public void An_older_date_this_year_reads_as_its_day_and_month()
    {
        RelativeTimeConverter.Format(new DateTimeOffset(2026, 4, 18, 0, 0, 0, TimeSpan.Zero), Now)
            .Should().Be("18 Apr");
    }

    [Test]
    public void A_date_from_another_year_keeps_its_year()
    {
        // The pump's install date read "18 Apr", as if it had been fitted this spring.
        RelativeTimeConverter.Format(new DateTimeOffset(2019, 4, 18, 0, 0, 0, TimeSpan.Zero), Now)
            .Should().Be("18 Apr 2019");
    }

    [Test]
    public async Task Evidence_is_stamped_by_the_app_clock()
    {
        using var harness = TestHarness.Create();

        var photo = await harness.Evidence.SavePhotoAsync("P-204-seal", [1, 2, 3], ".png", "Seal", default);

        photo.CreatedAt.Should().Be(harness.Time.GetLocalNow());
    }
}
