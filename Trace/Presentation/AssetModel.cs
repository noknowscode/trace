using Trace.Domain.Enums;
using Trace.Domain.Rules;
using Trace.Services;

namespace Trace.Presentation;

public enum AssetTab
{
    Overview = 0,
    Inspect = 1,
    History = 2,
    Documents = 3,
}

/// <summary>
/// The asset workspace: the machine, its points, and the inspection in progress.
/// </summary>
/// <remarks>
/// The editors (reading, note, disposition) are individual states so the view can bind them
/// two-way, and each one folds into a single persisted <see cref="Draft"/>. Validation is
/// derived from the point and that draft, which means the Complete button, the range
/// message and the failure prompt can never disagree with each other.
/// </remarks>
public partial record AssetModel
{
    private readonly AssetSummary _summary;
    private readonly IAssetRepository _assets;
    private readonly IInspectionRepository _inspections;
    private readonly ICorrectiveActionRepository _actions;
    private readonly IInspectionWorkflow _workflow;
    private readonly ICameraService _camera;
    private readonly ILocalEvidenceStore _evidence;
    private readonly INetworkStatusService _network;
    private readonly INavigator _navigator;
    private readonly NavigationOrigin _origin;
    private readonly FeedRecovery _recovery;
    private readonly TimeProvider _time;

    public AssetModel(
        AssetSummary summary,
        IAssetRepository assets,
        IInspectionRepository inspections,
        ICorrectiveActionRepository actions,
        IInspectionWorkflow workflow,
        ICameraService camera,
        ILocalEvidenceStore evidence,
        INetworkStatusService network,
        INavigator navigator,
        NavigationOrigin origin,
        FeedRecovery recovery,
        TimeProvider time)
    {
        _summary = summary;
        _assets = assets;
        _inspections = inspections;
        _actions = actions;
        _workflow = workflow;
        _camera = camera;
        _evidence = evidence;
        _network = network;
        _navigator = navigator;
        _origin = origin;
        _recovery = recovery;
        _time = time;

        // Each editor folds straight into the draft, and the draft is persisted on every
        // change, so walking away from a half-entered reading never loses it.
        ReadingText.ForEach(OnReadingChanged);
        NoteText.ForEach(OnNoteChanged);
        ChosenDisposition.ForEach(OnDispositionChanged);
        Draft.ForEach(PersistDraft);
    }

    public string AssetId => _summary.AssetId;

    public string Designation => _summary.Designation;

    public string Location => _summary.Location;

    public string AssetName => _summary.AssetName;

    public IFeed<Asset> Asset => _recovery.Watch(ct => _assets.Watch(_summary.AssetId, ct));

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();

    public IListFeed<InspectionPoint> Points =>
        Asset.Select(asset => asset is null
                ? ImmutableList<InspectionPoint>.Empty as IImmutableList<InspectionPoint>
                : asset.Points.OrderBy(p => p.Sequence).ToImmutableList())
            .AsListFeed();

    public IFeed<AssetKind> SchematicKind =>
        Asset.Select(asset => asset?.Kind ?? AssetKind.CentrifugalPump);

    /// <summary>The asset's own model file, or empty to draw the parametric equipment for its kind.</summary>
    public IFeed<string> ModelUri =>
        Asset.Select(asset => asset?.ModelUri ?? string.Empty);

    /// <summary>The points as the machine view needs them, carrying which one is open.</summary>
    public IListFeed<PointMarkerView> Markers =>
        Feed.Combine(Asset, SelectedPointId)
            .Select(pair => pair.Item1 is null
                ? ImmutableList<PointMarkerView>.Empty as IImmutableList<PointMarkerView>
                : pair.Item1.Points
                    .OrderBy(point => point.Sequence)
                    .Select(point => new PointMarkerView(point, point.Id == pair.Item2))
                    .ToImmutableList())
            .AsListFeed();

    public IListFeed<InspectionEvent> History =>
        _recovery.WatchList(ct => _inspections.WatchHistory(_summary.AssetId, ct));

    public IListFeed<CorrectiveAction> Findings =>
        _recovery.WatchList(ct => _actions.WatchForAsset(_summary.AssetId, ct));

    public IFeed<bool> IsOffline => Feed.AsyncEnumerable(_network.Watch).Select(online => !online);

    // ---- Which face of the asset is showing -------------------------------------------

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

    // ---- The selected point and the inspection in progress ----------------------------

    public IState<string> SelectedPointId => State.Value(this, () => string.Empty);

    public IFeed<InspectionPoint> SelectedPoint =>
        Feed.Combine(Asset, SelectedPointId)
            .Select(pair => pair.Item1 is null || string.IsNullOrEmpty(pair.Item2)
                ? null!
                : pair.Item1.FindPoint(pair.Item2)!);

    public IState<bool> IsInspectorOpen => State.Value(this, () => false);

    public IState<InspectionDraft> Draft => State<InspectionDraft>.Empty(this);

    public IState<string> ReadingText => State.Value(this, () => string.Empty);

    public IState<string> NoteText => State.Value(this, () => string.Empty);

    public IState<Disposition> ChosenDisposition => State.Value(this, () => Disposition.None);

    public IFeed<InspectionValidation> Validation =>
        Feed.Combine(SelectedPoint, Draft)
            .Select(pair => pair.Item1 is null || pair.Item2 is null
                ? null!
                : InspectionRules.Validate(pair.Item1, pair.Item2));

    public IFeed<bool> CanComplete => Validation.Select(v => v?.CanComplete == true);

    public IFeed<string> ValidationMessage => Validation.Select(v => v?.Message ?? string.Empty);

    public IFeed<bool> IsOutOfRange => Validation.Select(v => v?.IsOutOfRange == true);

    public IFeed<bool> HasBlocker => Validation.Select(v => v?.HasBlocker == true);

    public IFeed<bool> IsPassChosen => ChosenDisposition.Select(d => d == Disposition.Pass);

    public IFeed<bool> IsWatchChosen => ChosenDisposition.Select(d => d == Disposition.Watch);

    public IFeed<bool> IsFailChosen => ChosenDisposition.Select(d => d == Disposition.Fail);

    /// <summary>Set after a completion so the technician sees what was recorded.</summary>
    public IState<string> Confirmation => State<string>.Empty(this);

    public IFeed<bool> HasConfirmation => Confirmation.Select(text => !string.IsNullOrEmpty(text));

    /// <summary>Why the last Complete point recorded nothing, when the inspector does not already say.</summary>
    public IState<string> CompletionError => State<string>.Empty(this);

    public ValueTask DismissConfirmation(CancellationToken ct) =>
        Confirmation.UpdateAsync(_ => string.Empty, ct);

    /// <summary>
    /// Chooses a point. Resumes any saved draft for it rather than starting over, so an
    /// interrupted reading is still there when the technician comes back.
    /// </summary>
    public ValueTask SelectMarker(PointMarkerView marker, CancellationToken ct) =>
        marker is null ? ValueTask.CompletedTask : SelectPoint(marker.Point, ct);

    public async ValueTask SelectPoint(InspectionPoint point, CancellationToken ct)
    {
        if (point is null)
        {
            return;
        }

        var draft = await _workflow.StartOrResumeDraftAsync(point, ct);

        await SelectedPointId.UpdateAsync(_ => point.Id, ct);
        await LoadDraftAsync(draft, ct);
        await Confirmation.UpdateAsync(_ => string.Empty, ct);
        await CompletionError.UpdateAsync(_ => string.Empty, ct);
        await IsDiscardPending.UpdateAsync(_ => false, ct);

        // What was recorded at the previous point stays with that point: corrective work
        // raised from here must come from an inspection of this point.
        await RecordedEvent.UpdateAsync(_ => null!, ct);
        await ActionSummary.UpdateAsync(_ => string.Empty, ct);
        await ActionError.UpdateAsync(_ => string.Empty, ct);
        await IsActionSheetOpen.UpdateAsync(_ => false, ct);
        await IsInspectorOpen.UpdateAsync(_ => true, ct);
        await TabIndex.UpdateAsync(_ => (int)AssetTab.Inspect, ct);
    }

    public async ValueTask CloseInspector(CancellationToken ct)
    {
        await IsInspectorOpen.UpdateAsync(_ => false, ct);
        await IsActionSheetOpen.UpdateAsync(_ => false, ct);
        await IsDiscardPending.UpdateAsync(_ => false, ct);
        await SelectedPointId.UpdateAsync(_ => string.Empty, ct);
    }

    // ---- Discarding what was entered ----------------------------------------------------
    //
    // Leaving a point or closing the inspector keeps the draft, so nothing is lost and nothing
    // asks. Discard is the one action that throws entered work away, so it is the one that
    // confirms first (UNO-BUILD-SPEC: dirty entry confirms discard only when data would be lost).

    /// <summary>True once something has been entered at the open point that a discard would lose.</summary>
    public IFeed<bool> CanDiscard =>
        Feed.Combine(SelectedPoint, Draft)
            .Select(pair => pair.Item1 is not null && pair.Item2 is not null && pair.Item2.IsDirtyAgainst(pair.Item1));

    /// <summary>A discard is waiting for the technician to confirm it.</summary>
    public IState<bool> IsDiscardPending => State.Value(this, () => false);

    public ValueTask AskDiscard(CancellationToken ct) => IsDiscardPending.UpdateAsync(_ => true, ct);

    public ValueTask KeepDraft(CancellationToken ct) => IsDiscardPending.UpdateAsync(_ => false, ct);

    /// <summary>Drops what was entered at the open point, once confirmed, and starts it afresh.</summary>
    public async ValueTask DiscardDraft(CancellationToken ct)
    {
        await IsDiscardPending.UpdateAsync(_ => false, ct);

        var point = await SelectedPoint;
        if (point is null)
        {
            return;
        }

        await _workflow.DiscardDraftAsync(point.Id, ct);
        await LoadDraftAsync(await _workflow.StartOrResumeDraftAsync(point, ct), ct);
        await CompletionError.UpdateAsync(_ => string.Empty, ct);
    }

    public ValueTask SetPass(CancellationToken ct) => ChosenDisposition.UpdateAsync(_ => Disposition.Pass, ct);

    public ValueTask SetWatch(CancellationToken ct) => ChosenDisposition.UpdateAsync(_ => Disposition.Watch, ct);

    public ValueTask SetFail(CancellationToken ct) => ChosenDisposition.UpdateAsync(_ => Disposition.Fail, ct);

    public async ValueTask CompletePoint(CancellationToken ct)
    {
        var draft = await Draft;
        if (draft is null)
        {
            return;
        }

        CompletionOutcome outcome;
        try
        {
            outcome = await _workflow.CompleteAsync(draft, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The device could not write. The reading stays in the editor, and a second try
            // either records it or finds it already recorded: the token makes that safe.
            await CompletionError.UpdateAsync(
                _ => "This inspection could not be saved on the device. Try Complete point again; if it keeps failing, restart TRACE.",
                ct);
            return;
        }

        if (!outcome.Succeeded)
        {
            // Nothing was written. A blocker in the reading is usually on screen already, but
            // the point or asset may have gone, and then this is the only place it is said.
            await CompletionError.UpdateAsync(_ => outcome.Error ?? outcome.Validation.Message, ct);
            return;
        }

        await CompletionError.UpdateAsync(_ => string.Empty, ct);

        // The draft is finished work now, and the store has already dropped it. The editor
        // moves on to a fresh draft for the point: left holding the completed one, the next
        // edit would save it back, and every later Complete here would be a duplicate of
        // this inspection that records nothing.
        if (await SelectedPoint is { } open && open.Id == draft.PointId)
        {
            await LoadDraftAsync(await _workflow.StartOrResumeDraftAsync(open, ct), ct);
            await IsDiscardPending.UpdateAsync(_ => false, ct);
        }

        await RecordedEvent.UpdateAsync(_ => outcome.Event!, ct);
        await Confirmation.UpdateAsync(
            _ => outcome.WasDuplicate
                ? "Already recorded. This inspection was saved once."
                : $"{outcome.Event!.PointLabel} recorded as {outcome.Event.Disposition}. Saved on this device and queued to sync.",
            ct);

        var asset = await Asset;
        var point = asset?.FindPoint(outcome.Event!.PointId);
        await ActionSummary.UpdateAsync(
            _ => outcome.Event!.Disposition == Disposition.Fail && asset is not null && point is not null
                ? InspectionRules.SuggestActionSummary(asset, point)
                : string.Empty,
            ct);
    }

    // ---- Evidence ---------------------------------------------------------------------

    public IFeed<CameraPermission> CameraPermission => Feed.AsyncEnumerable(_camera.WatchPermission);

    public IFeed<bool> IsCameraDenied =>
        CameraPermission.Select(p => p == Services.CameraPermission.Denied);

    public IFeed<bool> HasEvidence => Draft.Select(draft => draft?.HasEvidence == true);

    public async ValueTask AttachPhoto(CancellationToken ct)
    {
        var point = await SelectedPoint;
        var draft = await Draft;
        if (point is null || draft is null)
        {
            return;
        }

        if (_camera.Permission != Services.CameraPermission.Granted
            && await _camera.RequestPermissionAsync(ct) != Services.CameraPermission.Granted)
        {
            // The denied state has its own recovery panel; nothing more to do here.
            return;
        }

        var capture = await _camera.CapturePhotoAsync(ct);
        if (capture is null)
        {
            return;
        }

        var saved = await _evidence.SavePhotoAsync(
            point.Id, capture.Content, capture.Extension, $"{point.Label} · {_time.GetLocalNow():t}", ct);

        await Draft.UpdateAsync(current => current is null ? null! : current with
        {
            Evidence = current.Evidence.Add(saved),
        }, ct);
    }

    /// <summary>
    /// The recovery path from a denied camera. On a device this opens system settings; here
    /// it completes the same round trip so the rest of the flow is exercised end to end.
    /// </summary>
    public ValueTask GrantCameraFromSettings(CancellationToken ct)
    {
        _camera.SetPermission(Services.CameraPermission.Granted);
        return ValueTask.CompletedTask;
    }

    public void DenyCameraForDemo() => _camera.SetPermission(Services.CameraPermission.Denied);

    // ---- Corrective action ------------------------------------------------------------

    /// <summary>
    /// The inspection just recorded at the open point. Corrective work is raised from this
    /// event, never from the editor, which has moved on to the next reading by then.
    /// </summary>
    public IState<InspectionEvent> RecordedEvent => State<InspectionEvent>.Empty(this);

    public IState<bool> IsActionSheetOpen => State.Value(this, () => false);

    public IState<string> ActionSummary => State.Value(this, () => string.Empty);

    public IState<string> ActionPriority => State.Value(this, () => nameof(CorrectivePriority.High));

    public IState<string> ActionAssignee => State.Value(this, () => "Mechanical Team");

    public IState<string> ActionError => State<string>.Empty(this);

    /// <summary>
    /// Mirrors the workflow's own rule so the button is dead until the action can succeed,
    /// the same way Complete point behaves. The workflow still rejects a blank summary —
    /// this is the affordance, not the enforcement.
    /// </summary>
    public IFeed<bool> CanCreateAction =>
        ActionSummary.Select(summary => !string.IsNullOrWhiteSpace(summary));

    public IFeed<IImmutableList<string>> Assignees =>
        Feed.Async(async ct => await _actions.GetAssigneesAsync(ct));

    public IFeed<IImmutableList<string>> Priorities => Feed.Async(_ => ValueTask.FromResult(
        (IImmutableList<string>)ImmutableList.Create(
            nameof(CorrectivePriority.Critical),
            nameof(CorrectivePriority.High),
            nameof(CorrectivePriority.Medium),
            nameof(CorrectivePriority.Low))));

    /// <summary>
    /// Corrective work can only be raised from an inspection that was recorded as failed:
    /// the call on the recorded event, not whatever the editor shows now.
    /// </summary>
    public IFeed<bool> CanRaiseAction =>
        RecordedEvent.Select(recorded => recorded?.Disposition == Disposition.Fail);

    public async ValueTask OpenActionSheet(CancellationToken ct)
    {
        await ActionError.UpdateAsync(_ => string.Empty, ct);
        await IsActionSheetOpen.UpdateAsync(_ => true, ct);
    }

    public ValueTask CancelAction(CancellationToken ct) => IsActionSheetOpen.UpdateAsync(_ => false, ct);

    public async ValueTask CreateAction(CancellationToken ct)
    {
        var recorded = await RecordedEvent;

        if (recorded is null || recorded.Disposition != Disposition.Fail)
        {
            return;
        }

        var priority = Enum.TryParse<CorrectivePriority>(await ActionPriority, out var parsed)
            ? parsed
            : CorrectivePriority.High;

        var outcome = await _workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(
                AssetId: _summary.AssetId,
                PointId: recorded.PointId,
                SourceInspectionEventId: recorded.Id,
                Summary: await ActionSummary ?? string.Empty,
                Priority: priority,
                Assignee: await ActionAssignee ?? "Mechanical Team"),
            ct);

        if (!outcome.Succeeded)
        {
            await ActionError.UpdateAsync(_ => outcome.Error ?? "The action could not be created.", ct);
            return;
        }

        await IsActionSheetOpen.UpdateAsync(_ => false, ct);
        await ActionError.UpdateAsync(_ => string.Empty, ct);
        await Confirmation.UpdateAsync(
            _ => $"Corrective action raised for {outcome.Action!.PointLabel} and assigned to {outcome.Action.Assignee}.",
            ct);
    }

    /// <summary>Where Back goes: the destination the asset was opened from (Today, Assets, Scan or Findings).</summary>
    public string BackRoute => $"../{_origin.Route}";

    public async ValueTask Back(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, BackRoute, cancellation: ct);

    // ---- Editor plumbing ---------------------------------------------------------------

    /// <summary>Puts a draft in the editor: the draft itself, then each field it feeds.</summary>
    private async ValueTask LoadDraftAsync(InspectionDraft draft, CancellationToken ct)
    {
        await Draft.UpdateAsync(_ => draft, ct);
        await ReadingText.UpdateAsync(_ => draft.ReadingText, ct);
        await NoteText.UpdateAsync(_ => draft.Notes, ct);
        await ChosenDisposition.UpdateAsync(_ => draft.Disposition, ct);
    }

    private async ValueTask OnReadingChanged(string? value, CancellationToken ct) =>
        await Draft.UpdateAsync(
            current => current is null ? null! : current with { ReadingText = value ?? string.Empty },
            ct);

    private async ValueTask OnNoteChanged(string? value, CancellationToken ct) =>
        await Draft.UpdateAsync(
            current => current is null ? null! : current with { Notes = value ?? string.Empty },
            ct);

    private async ValueTask OnDispositionChanged(Disposition value, CancellationToken ct) =>
        await Draft.UpdateAsync(
            current => current is null ? null! : current with { Disposition = value },
            ct);

    private async ValueTask PersistDraft(InspectionDraft? draft, CancellationToken ct)
    {
        if (draft is not null)
        {
            await _workflow.SaveDraftAsync(draft, ct);
        }
    }
}
