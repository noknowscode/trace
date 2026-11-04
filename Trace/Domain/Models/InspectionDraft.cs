namespace Trace.Domain.Models;

/// <summary>
/// Work in progress at one inspection point. Drafts are persisted, so leaving the point or
/// backgrounding the app does not lose a reading. <paramref name="ClientToken"/> is minted
/// once per draft and carried onto the resulting event to make completion idempotent.
/// </summary>
public partial record InspectionDraft(
    string ClientToken,
    string AssetId,
    string PointId,
    string ReadingText,
    Disposition Disposition,
    string Notes,
    IImmutableList<Evidence> Evidence)
{
    /// <summary>
    /// Starts a draft for a point, pre-filling the last known reading so a technician
    /// confirming an unchanged value does not have to retype it.
    /// </summary>
    public static InspectionDraft For(InspectionPoint point) => new(
        ClientToken: Guid.NewGuid().ToString("N"),
        AssetId: point.AssetId,
        PointId: point.Id,
        ReadingText: point.LatestReading is { } value
            ? (value == Math.Floor(value) ? value.ToString("0") : value.ToString("0.##"))
            : string.Empty,
        Disposition: Disposition.None,
        Notes: string.Empty,
        Evidence: ImmutableList<Evidence>.Empty);

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public bool HasEvidence => Evidence.Count > 0;

    /// <summary>True once the technician has entered anything worth confirming before discard.</summary>
    public bool IsDirtyAgainst(InspectionPoint point)
    {
        var pristine = For(point);
        return ReadingText != pristine.ReadingText
            || Disposition != Disposition.None
            || HasNotes
            || HasEvidence;
    }
}
