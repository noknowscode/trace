using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trace.Infrastructure.Repositories;
using Trace.Presentation;

namespace Trace.Tests;

/// <summary>The round: today's assets, what the device is holding, and the device controls.</summary>
public class TodayModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private TodayModel Today(IThemeService? theme = null, string? technician = "Alex R.") => new(
        new AssignmentRepository(_harness.Store),
        _harness.Network,
        _harness.Queue,
        theme ?? new FakeThemeService(),
        new FeedRecovery(),
        Options.Create(new AppConfig { Technician = technician }),
        _harness.Time,
        NullLogger<TodayModel>.Instance);

    [Test]
    public async Task The_round_lists_today_s_assets_in_order()
    {
        var model = Today();

        var round = await model.Assignments;

        round.Should().NotBeNull();
        round!.Select(a => a.AssetId).Should().Equal("P-204", "AHU-12", "C-07");
        round.Should().OnlyContain(a => a.IsScheduled);
    }

    [Test]
    public void The_header_names_the_technician_and_the_day()
    {
        Today().Technician.Should().Be("Alex R.");
        Today(technician: " ").Technician.Should().Be("Technician", "a device with no technician configured still reads");
        Today().Today.Should().Be("Wednesday 4 March", "the harness clock reads 4 March 2026");
    }

    [Test]
    public async Task Work_the_device_holds_shows_as_waiting_to_sync()
    {
        var model = Today();
        (await model.HasPending).Should().BeFalse("the seed has nothing queued");

        await CompletePassAtSealAsync();

        (await model.PendingCount).Should().Be(1);
        (await model.HasPending).Should().BeTrue();
        (await model.PendingLabel).Should().Be("1 change waiting to sync");
    }

    [Test]
    public async Task Going_offline_is_a_choice_the_round_offers_and_reflects()
    {
        var model = Today();
        (await model.IsOffline).Should().BeFalse();
        (await model.ConnectionAction).Should().Be("Go offline");

        model.ToggleConnection();

        (await model.IsOffline).Should().BeTrue();
        (await model.ConnectionAction).Should().Be("Go online");
    }

    [Test]
    public async Task The_theme_toggle_switches_and_its_label_offers_the_other_theme()
    {
        var theme = new FakeThemeService { Theme = AppTheme.Dark };
        var model = Today(theme);
        (await model.ThemeAction).Should().Be("Light theme");

        await model.ToggleTheme(default);

        theme.Requested.Should().Equal(AppTheme.Light);
        (await model.Theme).Should().Be(AppTheme.Light);
        (await model.ThemeAction).Should().Be("Dark theme");
        (await model.ThemeError).Should().BeNullOrEmpty();
    }

    [Test]
    public async Task A_theme_switch_that_fails_leaves_the_theme_and_says_so()
    {
        var theme = new FakeThemeService { Theme = AppTheme.Dark, Failure = new InvalidOperationException("Settings are read-only.") };
        var model = Today(theme);

        // The service used to throw out of the command, and the screen said nothing.
        await model.ToggleTheme(default);

        (await model.Theme).Should().Be(AppTheme.Dark);
        (await model.ThemeAction).Should().Be("Light theme");
        (await model.ThemeError).Should().Contain("could not be changed");

        // A switch the service declines (no window to apply it to) is reported the same way.
        theme.Failure = null;
        theme.Accepts = false;
        await model.ToggleTheme(default);
        (await model.Theme).Should().Be(AppTheme.Dark);
        (await model.ThemeError).Should().Contain("could not be changed");

        // The next switch that works clears the message.
        theme.Accepts = true;
        await model.ToggleTheme(default);
        (await model.Theme).Should().Be(AppTheme.Light);
        (await model.ThemeError).Should().BeNullOrEmpty();
    }

    private async Task CompletePassAtSealAsync()
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        (await _harness.Workflow.CompleteAsync(draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default))
            .Succeeded.Should().BeTrue();
    }
}

/// <summary>A theme service that records what was asked of it, and can be made to fail.</summary>
internal sealed class FakeThemeService : IThemeService
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public bool IsDark => Theme == AppTheme.Dark;

    /// <summary>Thrown by the next switch, when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>What a switch returns: false is the service's own "could not apply it".</summary>
    public bool Accepts { get; set; } = true;

    public List<AppTheme> Requested { get; } = [];

    public event EventHandler<AppTheme>? ThemeChanged;

    public Task<bool> SetThemeAsync(AppTheme theme)
    {
        Requested.Add(theme);
        if (Failure is { } failure)
        {
            return Task.FromException<bool>(failure);
        }

        if (Accepts)
        {
            Theme = theme;
            ThemeChanged?.Invoke(this, theme);
        }

        return Task.FromResult(Accepts);
    }

    public Task InitializeAsync() => Task.CompletedTask;
}
