namespace Trace.Domain.Models;

/// <summary>
/// Corrective work raised from a failed inspection. Asset, point and source event are
/// carried from the inspection so the technician never re-enters context.
/// </summary>
public partial record CorrectiveAction(
    string Id,
    string SourceInspectionEventId,
    string AssetId,
    string AssetName,
    string PointId,
    string PointLabel,
    CorrectivePriority Priority,
    string Summary,
    string Assignee,
    ActionStatus Status,
    DateTimeOffset CreatedAt)
{
    public string Origin => $"{AssetId} · {PointLabel}";

    /// <summary>Still work to do: raised or dispatched, not closed.</summary>
    public bool IsOpenWork => Status != ActionStatus.Closed;

    /// <summary>The status a supervisor moves it to next, or none once it is closed.</summary>
    public ActionStatus? NextStatus => InspectionRules.NextStatus(Status);

    public bool CanAdvance => NextStatus is not null;
}
