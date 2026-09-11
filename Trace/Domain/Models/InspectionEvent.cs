using Trace.Domain.Enums;

namespace Trace.Domain.Models;

/// <summary>
/// The immutable record of one completed inspection. <paramref name="ClientToken"/> makes
/// completion idempotent: a repeated submission of the same draft resolves to this event
/// rather than appending a duplicate.
/// </summary>
public partial record InspectionEvent(
    string Id,
    string ClientToken,
    string AssetId,
    string PointId,
    string PointLabel,
    DateTimeOffset Timestamp,
    string Technician,
    double Reading,
    string Unit,
    RangeStatus RangeStatus,
    Disposition Disposition,
    string Notes,
    IImmutableList<Evidence> Evidence,
    SyncState SyncState)
{
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public bool HasEvidence => Evidence.Count > 0;

    public string ReadingText => $"{(Reading == Math.Floor(Reading) ? Reading.ToString("0") : Reading.ToString("0.##"))} {Unit}";
}
