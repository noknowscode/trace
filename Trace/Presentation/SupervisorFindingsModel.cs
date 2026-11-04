namespace Trace.Presentation;

/// <summary>
/// The supervisor's view: everything a round turned up, worst first, with enough origin on
/// each row to dispatch it without opening the asset, and the step that moves it on.
/// </summary>
public partial record SupervisorFindingsModel
{
    private readonly ICorrectiveActionRepository _actions;
    private readonly IAssetRepository _assets;
    private readonly IActionDispatch _dispatch;
    private readonly FeedRecovery _recovery;

    public SupervisorFindingsModel(
        ICorrectiveActionRepository actions,
        IAssetRepository assets,
        IActionDispatch dispatch,
        FeedRecovery recovery)
    {
        _actions = actions;
        _assets = assets;
        _dispatch = dispatch;
        _recovery = recovery;
    }

    public IListFeed<CorrectiveAction> Actions => _recovery.WatchList(_actions.WatchAll);

    /// <summary>Assets that are failed or on watch, failed first, so a supervisor sees condition and work together.</summary>
    public IListFeed<AssetSummary> AtRisk =>
        _recovery.Watch(_assets.WatchSummaries)
            .Select(assets => assets
                .Where(a => a.Condition is AssetCondition.Failed or AssetCondition.Watch)
                .OrderByDescending(a => a.Condition)
                .ToImmutableList() as IImmutableList<AssetSummary>)
            .AsListFeed();

    public IFeed<string> Headline => _recovery.Watch(_actions.WatchAll).Select(actions =>
    {
        var open = actions.Count(a => a.IsOpenWork);
        var waiting = actions.Count(a => a.Status == ActionStatus.Open);
        var headline = open switch
        {
            0 => "No open corrective work.",
            1 => "1 corrective action is open.",
            _ => $"{open} corrective actions are open.",
        };

        return waiting switch
        {
            0 => headline,
            1 => $"{headline} 1 is waiting to be dispatched.",
            _ => $"{headline} {waiting} are waiting to be dispatched.",
        };
    });

    /// <summary>Why the last Dispatch or Close changed nothing.</summary>
    public IState<string> DispatchError => State<string>.Empty(this);

    /// <summary>
    /// Takes the row's next step: dispatches an open action to its assignee, or closes a
    /// dispatched one. The list follows the store, so the row moves on by itself.
    /// </summary>
    public async ValueTask Advance(CorrectiveAction action, CancellationToken ct)
    {
        if (action is null || !action.CanAdvance)
        {
            return;
        }

        CorrectiveActionOutcome outcome;
        try
        {
            outcome = await _dispatch.AdvanceAsync(action.Id, action.Status, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await DispatchError.UpdateAsync(
                _ => $"{action.Summary} could not be updated on the device. Try {ScreenText.NextStep(action)} again; if it keeps failing, restart TRACE.",
                ct);
            return;
        }

        await DispatchError.UpdateAsync(_ => outcome.Succeeded ? string.Empty : outcome.Error ?? string.Empty, ct);
    }

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();
}
