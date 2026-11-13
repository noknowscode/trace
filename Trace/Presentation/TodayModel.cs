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
    private readonly ILogger<TodayModel> _log;

    public TodayModel(
        IAssignmentRepository assignments,
        INetworkStatusService network,
        ISyncQueue sync,
        IThemeService theme,
        FeedRecovery recovery,
        IOptions<AppConfig> config,
        TimeProvider time,
        ILogger<TodayModel> log)
    {
        _assignments = assignments;
        _network = network;
        _sync = sync;
        _theme = theme;
        _recovery = recovery;
        _log = log;

        Technician = string.IsNullOrWhiteSpace(config.Value.Technician)
            ? "Technician"
            : config.Value.Technician;
        Today = time.GetLocalNow().ToString("dddd d MMMM");
    }

    public string Technician { get; }

    public string Today { get; }

    public IListFeed<AssetSummary> Assignments => _recovery.WatchList(_assignments.WatchToday);

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);

    public IFeed<bool> IsOffline => IsOnline.Select(online => !online);

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

    /// <summary>Why the last theme switch did not happen; empty once one has.</summary>
    public IState<string> ThemeError => State<string>.Empty(this);

    /// <summary>Dark for the field, light for the office. Both are first-class.</summary>
    /// <remarks>
    /// The theme service applies the theme to the window and saves the choice. When it cannot
    /// (no window to apply it to, or the settings could not be written), the theme on screen and
    /// the label stay as they were, and the round says so.
    /// </remarks>
    public async ValueTask ToggleTheme(CancellationToken ct)
    {
        var next = _theme.Theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;

        bool applied;
        try
        {
            applied = await _theme.SetThemeAsync(next);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "The theme could not be switched to {Theme}.", next);
            applied = false;
        }

        if (!applied)
        {
            await ThemeError.UpdateAsync(_ => ThemeNotChanged, ct);
            return;
        }

        await ThemeError.UpdateAsync(_ => string.Empty, ct);
        await Theme.SetAsync(next, ct);
    }

    private const string ThemeNotChanged =
        "The theme could not be changed on this device. Try again; your work is not affected.";
}
