using Trace.Domain.Enums;
using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// The supervisor's view: everything a round turned up, worst first, with enough origin on
/// each row to dispatch it without opening the asset.
/// </summary>
public partial record SupervisorFindingsModel
{
    private readonly ICorrectiveActionRepository _actions;
    private readonly IAssetRepository _assets;

    public SupervisorFindingsModel(ICorrectiveActionRepository actions, IAssetRepository assets)
    {
        _actions = actions;
        _assets = assets;
    }

    public IListFeed<CorrectiveAction> Actions => ListFeed.AsyncEnumerable(_actions.WatchAll);

    /// <summary>Assets that are failed or on watch, so a supervisor sees condition and work together.</summary>
    public IListFeed<AssetSummary> AtRisk =>
        Feed.AsyncEnumerable(_assets.WatchSummaries)
            .Select(assets => assets
                .Where(a => a.Condition is AssetCondition.Failed or AssetCondition.Watch)
                .ToImmutableList() as IImmutableList<AssetSummary>)
            .AsListFeed();

    public IFeed<string> Headline => Feed.AsyncEnumerable(_actions.WatchAll).Select(actions =>
    {
        var open = actions.Count(a => a.Status != ActionStatus.Complete);
        return open switch
        {
            0 => "No open corrective work.",
            1 => "1 corrective action is open.",
            _ => $"{open} corrective actions are open.",
        };
    });
}
