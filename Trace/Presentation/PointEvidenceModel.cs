namespace Trace.Presentation;

/// <summary>
/// The evidence at the open point: the photos on the draft, the photos last recorded there, and
/// the camera that takes them.
/// </summary>
/// <remarks>
/// A part of the point inspector, over the inspector's own open point and draft: a photo taken
/// here goes onto that draft, and the draft saves it like any other edit.
/// </remarks>
public partial record PointEvidenceModel
{
    private readonly IFeed<InspectionPoint> _point;
    private readonly IState<InspectionDraft> _draft;
    private readonly EvidenceViewerModel _viewer;
    private readonly ICameraService _camera;
    private readonly ILocalEvidenceStore _evidence;
    private readonly TimeProvider _time;

    /// <summary>
    /// The latest recorded inspection at the open point that carried photos, looked up once for
    /// every binding that shows it.
    /// </summary>
    private readonly IFeed<LastPhotographed> _lastPhotographed;

    /// <param name="pointId">The id of the point the inspector has open.</param>
    /// <param name="point">That point as the asset stores it.</param>
    /// <param name="draft">The inspector's draft at that point.</param>
    /// <param name="history">The asset's recorded inspections, newest first.</param>
    /// <param name="viewer">The workspace's photo viewer, which the photos recorded here open in.</param>
    public PointEvidenceModel(
        IFeed<string> pointId,
        IFeed<InspectionPoint> point,
        IState<InspectionDraft> draft,
        IFeed<IImmutableList<InspectionEvent>> history,
        EvidenceViewerModel viewer,
        ICameraService camera,
        ILocalEvidenceStore evidence,
        TimeProvider time)
    {
        _point = point;
        _draft = draft;
        _viewer = viewer;
        _camera = camera;
        _evidence = evidence;
        _time = time;

        _lastPhotographed = Feed.Combine(pointId, history)
            .Select(pair => new LastPhotographed(InspectionRules.LatestWithPhotos(pair.Item2, pair.Item1)));
    }

    // ---- The photos on the draft --------------------------------------------------------

    /// <summary>The photos attached to the draft, in the order they were taken.</summary>
    public IListFeed<Evidence> Photos => _draft.Select(draft => draft.Evidence).AsListFeed();

    public IFeed<bool> HasEvidence => _draft.Select(draft => draft.HasEvidence);

    /// <summary>Why the last Add photo attached nothing, when the device could not keep the picture.</summary>
    public IState<string> EvidenceError => State<string>.Empty(this);

    /// <summary>Forgets a message about the point that was open before.</summary>
    internal ValueTask ResetAsync(CancellationToken ct) => EvidenceError.UpdateAsync(_ => string.Empty, ct);

    public async ValueTask AttachPhoto(CancellationToken ct)
    {
        var point = await _point;
        var draft = await _draft;
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
        await _draft.UpdateAsync(current => current is null ? null : current with
        {
            Evidence = current.Evidence.Add(saved),
        }, ct);
    }

    /// <summary>
    /// Takes one photo off the draft and keeps the rest of what was entered. The draft is saved
    /// on change like every other edit; the picture's file stays on the device, as it does for a
    /// discarded draft.
    /// </summary>
    public async ValueTask RemovePhoto(string photoId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(photoId))
        {
            return;
        }

        await _draft.UpdateAsync(current => current is null ? null : current with
        {
            Evidence = current.Evidence.RemoveAll(item => item.Id == photoId),
        }, ct);
    }

    // ---- The photos recorded here before ------------------------------------------------

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

    // ---- The camera ---------------------------------------------------------------------

    public IFeed<CameraPermission> CameraPermission => Feed.AsyncEnumerable(_camera.WatchPermission);

    public IFeed<bool> IsCameraDenied =>
        CameraPermission.Select(p => p == Services.CameraPermission.Denied);

    /// <summary>
    /// The recovery path from a denied camera. On a device this opens system settings; here
    /// it completes the same round trip so the rest of the flow is exercised end to end.
    /// </summary>
    public void GrantCameraFromSettings() => _camera.SetPermission(Services.CameraPermission.Granted);

    public void DenyCameraForDemo() => _camera.SetPermission(Services.CameraPermission.Denied);

    /// <summary>The latest inspection at the open point that carried photos, and those photos.</summary>
    private sealed record LastPhotographed(InspectionEvent? Event)
    {
        public IImmutableList<Evidence> Photos { get; } = Event is null
            ? ImmutableList<Evidence>.Empty
            : Event.Evidence.Where(item => item.Type == EvidenceType.Photo).ToImmutableList();
    }
}
