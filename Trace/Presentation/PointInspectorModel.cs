namespace Trace.Presentation;

/// <summary>
/// The inspection in progress at one point of the open asset: the reading, the call, the note
/// and the evidence, and the completion that records them.
/// </summary>
/// <remarks>
/// The editors (reading, note, disposition) are individual states so the view can bind them
/// two-way, and each one folds into a single persisted <see cref="Draft"/>. Validation is
/// derived from the point and that draft, which means the Complete button, the range message
/// and the failure prompt can never disagree with each other.
/// </remarks>
public partial record PointInspectorModel
{
    private readonly IFeed<Asset> _asset;
    private readonly IFeed<IImmutableList<InspectionEvent>> _history;
    private readonly CorrectiveActionSheetModel _actionSheet;
    private readonly EvidenceViewerModel _viewer;
    private readonly IInspectionWorkflow _workflow;
    private readonly ICameraService _camera;
    private readonly ILocalEvidenceStore _evidence;
    private readonly TimeProvider _time;

    /// <summary>
    /// The latest recorded inspection at the open point that carried photos, looked up once for
    /// every binding that shows it.
    /// </summary>
    private readonly IFeed<LastPhotographed> _lastPhotographed;

    /// <param name="asset">The open asset, as the workspace reads it: the point shown is always its stored self.</param>
    /// <param name="history">The asset's recorded inspections, newest first.</param>
    /// <param name="actionSheet">The sheet corrective work is raised on, from what is recorded here.</param>
    /// <param name="viewer">The workspace's photo viewer, which the photos recorded here open in.</param>
    public PointInspectorModel(
        IFeed<Asset> asset,
        IFeed<IImmutableList<InspectionEvent>> history,
        CorrectiveActionSheetModel actionSheet,
        EvidenceViewerModel viewer,
        IInspectionWorkflow workflow,
        ICameraService camera,
        ILocalEvidenceStore evidence,
        TimeProvider time)
    {
        _asset = asset;
        _history = history;
        _actionSheet = actionSheet;
        _viewer = viewer;
        _workflow = workflow;
        _camera = camera;
        _evidence = evidence;
        _time = time;

        _lastPhotographed = Feed.Combine(SelectedPointId, _history)
            .Select(pair => new LastPhotographed(InspectionRules.LatestWithPhotos(pair.Item2, pair.Item1)));

        // Each editor folds straight into the draft, and the draft is persisted on every
        // change, so walking away from a half-entered reading never loses it.
        ReadingText.ForEach(OnReadingChanged);
        NoteText.ForEach(OnNoteChanged);
        ChosenDisposition.ForEach(OnDispositionChanged);
        Draft.ForEach(PersistDraft);
        _actionSheet.Raised.ForEach(OnActionRaised);
    }

    // ---- The point that is open --------------------------------------------------------

    public IState<string> SelectedPointId => State.Value(this, () => string.Empty);

    /// <summary>The open point as the asset stores it, or None while no point is open or it has gone.</summary>
    public IFeed<InspectionPoint> SelectedPoint =>
        Feed.Combine(_asset, SelectedPointId)
            .SelectOrNone(pair => pair.Item1 is null || string.IsNullOrEmpty(pair.Item2)
                ? null
                : pair.Item1.FindPoint(pair.Item2));

    public IState<bool> IsOpen => State.Value(this, () => false);

    /// <summary>
    /// Opens a point. Resumes any saved draft for it rather than starting over, so an
    /// interrupted reading is still there when the technician comes back, and offers corrective
    /// work again for a failure at the point that has none yet.
    /// </summary>
    internal async ValueTask OpenAsync(InspectionPoint point, CancellationToken ct)
    {
        await DraftError.UpdateAsync(_ => string.Empty, ct);
        var draft = await StartOrResumeDraftAsync(point, ct);

        await SelectedPointId.UpdateAsync(_ => point.Id, ct);
        await LoadDraftAsync(draft, ct);
        await Confirmation.UpdateAsync(_ => string.Empty, ct);
        await CompletionError.UpdateAsync(_ => string.Empty, ct);
        await EvidenceError.UpdateAsync(_ => string.Empty, ct);
        await IsDiscardPending.UpdateAsync(_ => false, ct);

        // What was recorded at the previous point stays with that point: corrective work
        // raised from here must come from an inspection of this point. Its latest inspection,
        // when it failed and no work was raised from it, still offers Create corrective action:
        // closing the inspector before Create used to lose the offer for good.
        var awaitingAction = await FailureAwaitingActionAsync(point, ct);
        await RecordedEvent.UpdateAsync(_ => awaitingAction, ct);
        await _actionSheet.ResetAsync(ct);
        await IsOpen.UpdateAsync(_ => true, ct);
    }

    public async ValueTask Close(CancellationToken ct)
    {
        await IsOpen.UpdateAsync(_ => false, ct);
        await _actionSheet.Cancel(ct);
        await IsDiscardPending.UpdateAsync(_ => false, ct);
        await SelectedPointId.UpdateAsync(_ => string.Empty, ct);
    }

    // ---- The reading, the call and the note --------------------------------------------

    public IState<InspectionDraft> Draft => State<InspectionDraft>.Empty(this);

    public IState<string> ReadingText => State.Value(this, () => string.Empty);

    public IState<string> NoteText => State.Value(this, () => string.Empty);

    public IState<Disposition> ChosenDisposition => State.Value(this, () => Disposition.None);

    public IFeed<InspectionValidation> Validation =>
        Feed.Combine(SelectedPoint, Draft)
            .SelectOrNone(pair => pair.Item1 is null || pair.Item2 is null
                ? null
                : InspectionRules.Validate(pair.Item1, pair.Item2));

    public IFeed<bool> CanComplete => Validation.Select(v => v?.CanComplete == true);

    public IFeed<string> ValidationMessage => Validation.Select(v => v?.Message ?? string.Empty);

    public IFeed<bool> IsOutOfRange => Validation.Select(v => v?.IsOutOfRange == true);

    public IFeed<bool> HasBlocker => Validation.Select(v => v?.HasBlocker == true);

    public IFeed<bool> IsPassChosen => ChosenDisposition.Select(d => d == Disposition.Pass);

    public IFeed<bool> IsWatchChosen => ChosenDisposition.Select(d => d == Disposition.Watch);

    public IFeed<bool> IsFailChosen => ChosenDisposition.Select(d => d == Disposition.Fail);

    public ValueTask SetPass(CancellationToken ct) => ChosenDisposition.UpdateAsync(_ => Disposition.Pass, ct);

    public ValueTask SetWatch(CancellationToken ct) => ChosenDisposition.UpdateAsync(_ => Disposition.Watch, ct);

    public ValueTask SetFail(CancellationToken ct) => ChosenDisposition.UpdateAsync(_ => Disposition.Fail, ct);

    // ---- Completing the point ----------------------------------------------------------

    /// <summary>Set after a completion so the technician sees what was recorded.</summary>
    public IState<string> Confirmation => State<string>.Empty(this);

    public IFeed<bool> HasConfirmation => Confirmation.Select(text => !string.IsNullOrEmpty(text));

    /// <summary>Why the last Complete point recorded nothing, when the inspector does not already say.</summary>
    public IState<string> CompletionError => State<string>.Empty(this);

    /// <summary>
    /// Why the draft at this point is not on the device: it could not be read, saved or
    /// discarded there. The editor keeps working from memory meanwhile.
    /// </summary>
    public IState<string> DraftError => State<string>.Empty(this);

    public ValueTask DismissConfirmation(CancellationToken ct) =>
        Confirmation.UpdateAsync(_ => string.Empty, ct);

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
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            // The device could not write. The reading stays in the editor, and a second try
            // either records it or finds it already recorded: the token makes that safe.
            await CompletionError.UpdateAsync(
                _ => "This inspection could not be saved on the device. Try Complete point again; if it keeps failing, restart TRACE.",
                ct);
            return;
        }

        if (!outcome.Succeeded || outcome.Event is not { } recorded)
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
            await LoadDraftAsync(await StartOrResumeDraftAsync(open, ct), ct);
            await IsDiscardPending.UpdateAsync(_ => false, ct);
        }

        await RecordedEvent.UpdateAsync(_ => recorded, ct);
        await Confirmation.UpdateAsync(
            _ => outcome.WasDuplicate
                ? "Already recorded. This inspection was saved once."
                : $"{recorded.PointLabel} recorded as {recorded.Disposition}. Saved on this device and queued to sync.",
            ct);
    }

    // ---- Discarding what was entered ---------------------------------------------------
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

        try
        {
            await _workflow.DiscardDraftAsync(point.Id, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            // Nothing was thrown away: the entry stays in the editor and on the device.
            await DraftError.UpdateAsync(
                _ => "What was entered here could not be discarded on the device. Try Discard again; if it keeps failing, restart TRACE.",
                ct);
            return;
        }

        await DraftError.UpdateAsync(_ => string.Empty, ct);
        await LoadDraftAsync(await StartOrResumeDraftAsync(point, ct), ct);
        await CompletionError.UpdateAsync(_ => string.Empty, ct);
    }

    // ---- Evidence ----------------------------------------------------------------------

    /// <summary>
    /// The latest recorded inspection at the open point that carried photos: what the point
    /// looked like the last time someone photographed it, to judge today's reading against.
    /// </summary>
    public IFeed<InspectionEvent> LastEvidence => _lastPhotographed.SelectOrNone(last => last.Event);

    /// <summary>
    /// The photos of <see cref="LastEvidence"/>, as a list of the records themselves: each opens
    /// the viewer (<see cref="ViewEvidence"/>) with its own <see cref="Evidence"/> as the command parameter.
    /// </summary>
    public IListFeed<Evidence> LastPhotos => _lastPhotographed.Select(last => last.Photos).AsListFeed();

    public IFeed<bool> HasLastEvidence => _lastPhotographed.Select(last => last.Event is not null);

    /// <summary>Opens one of the photos last recorded here full size, in the workspace's viewer.</summary>
    public ValueTask ViewEvidence(Evidence evidence, CancellationToken ct) =>
        evidence is null ? ValueTask.CompletedTask : _viewer.OpenAsync(evidence, ct);

    public IFeed<CameraPermission> CameraPermission => Feed.AsyncEnumerable(_camera.WatchPermission);

    public IFeed<bool> IsCameraDenied =>
        CameraPermission.Select(p => p == Services.CameraPermission.Denied);

    public IFeed<bool> HasEvidence => Draft.Select(draft => draft?.HasEvidence == true);

    /// <summary>Why the last Add photo attached nothing, when the device could not keep the picture.</summary>
    public IState<string> EvidenceError => State<string>.Empty(this);

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

        Evidence saved;
        try
        {
            var capture = await _camera.CapturePhotoAsync(ct);
            if (capture is null)
            {
                return;
            }

            saved = await _evidence.SavePhotoAsync(
                point.Id, capture.Content, capture.Extension, $"{point.Label} · {_time.GetLocalNow():t}", ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await EvidenceError.UpdateAsync(
                _ => "The photo could not be saved on the device, so nothing was attached. Try Add photo again; a written note also satisfies a failed point.",
                ct);
            return;
        }

        await EvidenceError.UpdateAsync(_ => string.Empty, ct);
        await Draft.UpdateAsync(current => current is null ? null : current with
        {
            Evidence = current.Evidence.Add(saved),
        }, ct);
    }

    /// <summary>
    /// The recovery path from a denied camera. On a device this opens system settings; here
    /// it completes the same round trip so the rest of the flow is exercised end to end.
    /// </summary>
    public void GrantCameraFromSettings() => _camera.SetPermission(Services.CameraPermission.Granted);

    public void DenyCameraForDemo() => _camera.SetPermission(Services.CameraPermission.Denied);

    // ---- Corrective work from what was recorded here ------------------------------------

    /// <summary>
    /// The inspection just recorded at the open point. Corrective work is raised from this
    /// event, never from the editor, which has moved on to the next reading by then.
    /// </summary>
    public IState<InspectionEvent> RecordedEvent => State<InspectionEvent>.Empty(this);

    /// <summary>
    /// Corrective work can only be raised from an inspection that was recorded as failed:
    /// the call on the recorded event, not whatever the editor shows now.
    /// </summary>
    public IFeed<bool> CanRaiseAction =>
        RecordedEvent.Select(recorded => recorded?.Disposition == Disposition.Fail);

    public async ValueTask OpenActionSheet(CancellationToken ct)
    {
        if (await RecordedEvent is { Disposition: Disposition.Fail } recorded)
        {
            await _actionSheet.OpenAsync(recorded, ScreenText.SuggestedActionSummary(recorded), ct);
        }
    }

    /// <summary>
    /// The sheet raised work from the inspection recorded here. That inspection has its action
    /// now, so Create corrective action is no longer offered for it: a second Create used to
    /// raise a duplicate.
    /// </summary>
    private async ValueTask OnActionRaised(CorrectiveAction? action, CancellationToken ct)
    {
        if (action is null)
        {
            return;
        }

        if (await RecordedEvent is { } recorded && recorded.Id == action.SourceInspectionEventId)
        {
            await RecordedEvent.UpdateAsync(_ => null, ct);
        }

        await Confirmation.UpdateAsync(_ => ScreenText.ActionRaised(action), ct);
    }

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
            current => current is null ? null : current with { ReadingText = value ?? string.Empty },
            ct);

    private async ValueTask OnNoteChanged(string? value, CancellationToken ct) =>
        await Draft.UpdateAsync(
            current => current is null ? null : current with { Notes = value ?? string.Empty },
            ct);

    private async ValueTask OnDispositionChanged(Disposition value, CancellationToken ct) =>
        await Draft.UpdateAsync(
            current => current is null ? null : current with { Disposition = value },
            ct);

    /// <summary>
    /// The saved draft for <paramref name="point"/>, or a new one. When the device cannot read
    /// or keep it, the point still opens, on a draft held in memory, and the inspector says so.
    /// </summary>
    private async ValueTask<InspectionDraft> StartOrResumeDraftAsync(InspectionPoint point, CancellationToken ct)
    {
        try
        {
            return await _workflow.StartOrResumeDraftAsync(point, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await DraftError.UpdateAsync(_ => DraftNotSaved, ct);
            return InspectionDraft.For(point);
        }
    }

    /// <summary>
    /// The latest inspection at <paramref name="point"/> when it failed and no corrective work was
    /// raised from it. When the device cannot read its records the point opens without the offer;
    /// the draft message above already says the device could not be reached.
    /// </summary>
    private async ValueTask<InspectionEvent?> FailureAwaitingActionAsync(InspectionPoint point, CancellationToken ct)
    {
        try
        {
            return await _workflow.FindFailureAwaitingActionAsync(point.Id, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            return null;
        }
    }

    private async ValueTask PersistDraft(InspectionDraft? draft, CancellationToken ct)
    {
        if (draft is null)
        {
            return;
        }

        try
        {
            await _workflow.SaveDraftAsync(draft, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await DraftError.UpdateAsync(_ => DraftNotSaved, ct);
            return;
        }

        // Saved: a message left by an earlier failed save is no longer true.
        if (await DraftError == DraftNotSaved)
        {
            await DraftError.UpdateAsync(_ => string.Empty, ct);
        }
    }

    private const string DraftNotSaved =
        "This entry could not be saved on the device, so it would be lost if TRACE closed. Keep going: Complete point tries the device again.";

    /// <summary>The latest inspection at the open point that carried photos, and those photos.</summary>
    private sealed record LastPhotographed(InspectionEvent? Event)
    {
        public IImmutableList<Evidence> Photos { get; } = Event is null
            ? ImmutableList<Evidence>.Empty
            : Event.Evidence.Where(item => item.Type == EvidenceType.Photo).ToImmutableList();
    }
}
