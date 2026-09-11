namespace Trace.Domain.Models;

/// <summary>
/// A scheduled visit to an asset on the technician's round.
/// </summary>
public partial record Assignment(
    string Id,
    string AssetId,
    int Sequence,
    string Window,
    string Reason,
    DateTimeOffset DueAt);
