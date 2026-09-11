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
    private readonly IAppThemeService _theme;

    public TodayModel(
        IAssignmentRepository assignments,
        INetworkStatusService network,
        ISyncQueue sync,
        IAppThemeService theme,
        IOptions<AppConfig> config)
    {
        _assignments = assignments;
        _network = network;
        _sync = sync;
        _theme = theme;

        Technician = string.IsNullOrWhiteSpace(config.Value.Technician)
            ? "Technician"
            : config.Value.Technician!;
        Today = DateTimeOffset.Now.ToString("dddd d MMMM");
    }

    public string Technician { get; }

    public string Today { get; }

    public IListFeed<AssetSummary> Assignments => ListFeed.AsyncEnumerable(_assignments.WatchToday);

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);

    public IFeed<bool> IsOffline => IsOnline.Select(online => !online);

    public IFeed<string> ConnectionLabel => IsOnline.Select(online => online ? "Online" : "Offline");

    public IFeed<int> PendingCount => Feed.AsyncEnumerable(_sync.WatchPendingCount);

    public IFeed<bool> HasPending => PendingCount.Select(count => count > 0);

    public IFeed<string> PendingLabel => PendingCount.Select(count =>
        count == 1 ? "1 change waiting to sync" : $"{count} changes waiting to sync");

    public IFeed<string> ConnectionAction => IsOnline.Select(online => online ? "Go offline" : "Go online");

    public IFeed<string> ThemeAction =>
        Feed.AsyncEnumerable(_theme.Watch).Select(dark => dark ? "Light theme" : "Dark theme");

    /// <summary>
    /// Declares the device offline before walking into a dead zone, or back online after.
    /// Offline is a working mode the technician chooses, not only a condition detected.
    /// </summary>
    public void ToggleConnection() => _network.SetOffline(_network.IsOnline);

    /// <summary>Dark for the field, light for the office. Both are first-class.</summary>
    public void ToggleTheme() => _theme.Toggle();
}
