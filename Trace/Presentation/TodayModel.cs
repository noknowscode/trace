using Microsoft.Extensions.Options;
using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// The round for today. Everything here is a live projection of the store, so completing a
/// point anywhere in the app moves the progress on this list without a refresh.
/// </summary>
public partial record TodayModel
{
    private readonly IAssignmentRepository _assignments;
    private readonly INetworkStatusService _network;
    private readonly ISyncQueue _sync;
    private readonly IThemeService _theme;
    private readonly FeedRecovery _recovery;

    public TodayModel(
        IAssignmentRepository assignments,
        INetworkStatusService network,
        ISyncQueue sync,
        IThemeService theme,
        FeedRecovery recovery,
        IOptions<AppConfig> config)
    {
        _assignments = assignments;
        _network = network;
        _sync = sync;
        _theme = theme;
        _recovery = recovery;

        Technician = string.IsNullOrWhiteSpace(config.Value.Technician)
            ? "Technician"
            : config.Value.Technician!;
        Today = DateTimeOffset.Now.ToString("dddd d MMMM");
    }

    public string Technician { get; }

    public string Today { get; }

    public IListFeed<AssetSummary> Assignments => _recovery.WatchList(_assignments.WatchToday);

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);

    public IFeed<bool> IsOffline => IsOnline.Select(online => !online);

    public IFeed<string> ConnectionLabel => IsOnline.Select(online => online ? "Online" : "Offline");

    public IFeed<int> PendingCount => _recovery.Watch(_sync.WatchPendingCount);

    public IFeed<bool> HasPending => PendingCount.Select(count => count > 0);

    public IFeed<string> PendingLabel => PendingCount.Select(count =>
        count == 1 ? "1 change waiting to sync" : $"{count} changes waiting to sync");

    public IFeed<string> ConnectionAction => IsOnline.Select(online => online ? "Go offline" : "Go online");

    public IState<AppTheme> Theme => State.Value(this, () => _theme.Theme);

    public IFeed<string> ThemeAction =>
        Theme.Select(theme => theme == AppTheme.Light ? "Dark theme" : "Light theme");

    /// <summary>
    /// Declares the device offline before walking into a dead zone, or back online after.
    /// Offline is a working mode the technician chooses, not only a condition detected.
    /// </summary>
    public void ToggleConnection() => _network.SetOffline(_network.IsOnline);

    /// <summary>Dark for the field, light for the office. Both are first-class.</summary>
    public async ValueTask ToggleTheme(CancellationToken ct)
    {
        var next = _theme.Theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        await _theme.SetThemeAsync(next);
        await Theme.SetAsync(next, ct);
    }
}
