namespace Trace.Presentation;

public enum AssetTab
{
    Overview = 0,
    Inspect = 1,
    History = 2,
    Documents = 3,
}

/// <summary>
/// The asset workspace: the machine, its points, its tabs, and the way back to where it was
/// opened from.
/// </summary>
/// <remarks>
/// The inspection at a point, the corrective-action sheet and the photo viewer are models of
/// their own (<see cref="PointInspectorModel"/>, <see cref="CorrectiveActionSheetModel"/>,
/// <see cref="EvidenceViewerModel"/>), each bound to its own control. The container builds them
/// (<see cref="AssetWorkspaceParts"/>); the workspace gives the inspector its own record of the
/// asset, so all of them read the same machine, and hands it a point when one is chosen.
/// </remarks>
public partial record AssetModel
{
    private readonly AssetSummary _summary;
    private readonly IAssetRepository _assets;
    private readonly IInspectionRepository _inspections;
    private readonly ICorrectiveActionRepository _actions;
    private readonly INetworkStatusService _network;
    private readonly INavigator _navigator;
    private readonly NavigationOrigin _origin;
    private readonly FeedRecovery _recovery;

    public AssetModel(
        AssetSummary summary,
        IAssetRepository assets,
        IInspectionRepository inspections,
        ICorrectiveActionRepository actions,
        INetworkStatusService network,
        INavigator navigator,
        NavigationOrigin origin,
        FeedRecovery recovery,
        AssetWorkspaceParts parts)
    {
        _summary = summary;
        _assets = assets;
        _inspections = inspections;
        _actions = actions;
        _network = network;
        _navigator = navigator;
        _origin = origin;
        _recovery = recovery;

        ActionSheetModel = parts.ActionSheet();
        ViewerModel = parts.Viewer();
        InspectorModel = parts.Inspector(
            Asset,
            _recovery.Watch(ct => _inspections.WatchHistory(_summary.AssetId, ct)),
            ActionSheetModel,
            ViewerModel);
    }

    /// <summary>The inspection at the open point (bound through <see cref="AssetViewModel.Inspector"/>).</summary>
    internal PointInspectorModel InspectorModel { get; }

    /// <summary>Corrective work raised from the open point (bound through <see cref="AssetViewModel.ActionSheet"/>).</summary>
    internal CorrectiveActionSheetModel ActionSheetModel { get; }

    /// <summary>The recorded photo shown full size (bound through <see cref="AssetViewModel.Viewer"/>).</summary>
    internal EvidenceViewerModel ViewerModel { get; }

    public string AssetId => _summary.AssetId;

    public string Designation => _summary.Designation;

    public string Location => _summary.Location;

    public string AssetName => _summary.AssetName;

    public IFeed<Asset> Asset => _recovery.Watch(ct => _assets.Watch(_summary.AssetId, ct));

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();

    public IFeed<AssetKind> SchematicKind =>
        Asset.Select(asset => asset?.Kind ?? AssetKind.CentrifugalPump);

    /// <summary>The asset's own model file, or empty to draw the parametric equipment for its kind.</summary>
    public IFeed<string> ModelUri =>
        Asset.Select(asset => asset?.ModelUri ?? string.Empty);

    /// <summary>The points as the machine view needs them, carrying which one is open.</summary>
    public IListFeed<PointMarkerView> Markers =>
        Feed.Combine(Asset, InspectorModel.SelectedPointId)
            .Select(pair => pair.Item1 is null
                ? ImmutableList<PointMarkerView>.Empty as IImmutableList<PointMarkerView>
                : pair.Item1.Points
                    .OrderBy(point => point.Sequence)
                    .Select(point => new PointMarkerView(point, point.Id == pair.Item2))
                    .ToImmutableList())
            .AsListFeed();

    public IListFeed<InspectionEvent> History =>
        _recovery.WatchList(ct => _inspections.WatchHistory(_summary.AssetId, ct));

    /// <summary>The asset's corrective work that is not closed yet.</summary>
    public IListFeed<CorrectiveAction> Findings =>
        _recovery.WatchList(ct => _actions.WatchOpenForAsset(_summary.AssetId, ct));

    public IFeed<bool> IsOffline => Feed.AsyncEnumerable(_network.Watch).Select(online => !online);

    /// <summary>
    /// Which face of the asset is showing. Held as the tab strip's index so the control can
    /// drive it two-way; Inspect is the landing tab because the machine is the task, not a
    /// record about it.
    /// </summary>
    public IState<int> TabIndex => State.Value(this, () => (int)AssetTab.Inspect);

    public IFeed<bool> IsOverview => TabIndex.Select(index => index == (int)AssetTab.Overview);

    public IFeed<bool> IsInspect => TabIndex.Select(index => index == (int)AssetTab.Inspect);

    /// <summary>
    /// The machine is drawn only once its record has been read. Until then, after a failed
    /// read, or when the asset has left the device, the Inspect tab shows that state instead
    /// of a default pump with no points.
    /// </summary>
    public IFeed<bool> ShowsMachine =>
        Feed.Combine(IsInspect, Asset).Select(pair => pair.Item1 && pair.Item2 is not null);

    public IFeed<bool> IsHistory => TabIndex.Select(index => index == (int)AssetTab.History);

    public IFeed<bool> IsDocuments => TabIndex.Select(index => index == (int)AssetTab.Documents);

    /// <summary>A marker on the machine or a chip under it was pressed.</summary>
    public ValueTask SelectMarker(PointMarkerView marker, CancellationToken ct) =>
        marker is null ? ValueTask.CompletedTask : SelectPoint(marker.Point, ct);

    /// <summary>Opens a point in the inspector, on the Inspect tab.</summary>
    public async ValueTask SelectPoint(InspectionPoint point, CancellationToken ct)
    {
        if (point is null)
        {
            return;
        }

        await InspectorModel.OpenAsync(point, ct);
        await TabIndex.UpdateAsync(_ => (int)AssetTab.Inspect, ct);
    }

    /// <summary>Opens a photo from History full size, in the workspace's viewer.</summary>
    public ValueTask ViewEvidence(Evidence evidence, CancellationToken ct) =>
        evidence is null ? ValueTask.CompletedTask : ViewerModel.OpenAsync(evidence, ct);

    /// <summary>Where Back goes: the destination the asset was opened from (Today, Assets, Scan or Findings).</summary>
    public string BackRoute => $"../{_origin.Route}";

    public async ValueTask Back(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, BackRoute, cancellation: ct);
}
