namespace Trace.Presentation;

/// <summary>
/// Builds the parts of an asset workspace: the point inspector with its evidence, the
/// corrective-action sheet and the photo viewer.
/// </summary>
/// <remarks>
/// Registered in the container, so the services the parts work with (the workflow, the camera,
/// the evidence store, the clock) come from it and never pass through the workspace. The workspace
/// hands over only what is its own: its record of the asset and of its history, which the
/// inspector reads, and the sheet and viewer the inspector works with.
/// </remarks>
public sealed class AssetWorkspaceParts
{
    private readonly IInspectionWorkflow _workflow;
    private readonly ICorrectiveActionRepository _actions;
    private readonly ICameraService _camera;
    private readonly ILocalEvidenceStore _evidence;
    private readonly TimeProvider _time;

    public AssetWorkspaceParts(
        IInspectionWorkflow workflow,
        ICorrectiveActionRepository actions,
        ICameraService camera,
        ILocalEvidenceStore evidence,
        TimeProvider time)
    {
        _workflow = workflow;
        _actions = actions;
        _camera = camera;
        _evidence = evidence;
        _time = time;
    }

    /// <summary>A sheet for raising corrective work from a failed inspection.</summary>
    public CorrectiveActionSheetModel ActionSheet() => new(_workflow, _actions);

    /// <summary>A viewer for recorded photos, shown over the whole workspace.</summary>
    public EvidenceViewerModel Viewer() => new();

    /// <summary>An inspector, with its evidence part over the inspector's own point and draft.</summary>
    /// <param name="asset">The open asset, as the workspace reads it.</param>
    /// <param name="history">The asset's recorded inspections, newest first.</param>
    /// <param name="actionSheet">The sheet corrective work is raised on.</param>
    /// <param name="viewer">The viewer the inspector's recorded photos open in.</param>
    public PointInspectorModel Inspector(
        IFeed<Asset> asset,
        IFeed<IImmutableList<InspectionEvent>> history,
        CorrectiveActionSheetModel actionSheet,
        EvidenceViewerModel viewer) =>
        new(asset, actionSheet, _workflow, inspector => new PointEvidenceModel(
            inspector.SelectedPointId,
            inspector.SelectedPoint,
            inspector.Draft,
            history,
            viewer,
            _camera,
            _evidence,
            _time));
}
