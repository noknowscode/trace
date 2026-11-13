namespace Trace.Presentation.Controls;

/// <summary>
/// How loudly a piece of status should read. One scale covers dispositions, asset
/// conditions, sync states, action statuses and priorities, so the same colour always means
/// the same thing wherever it appears.
/// </summary>
public enum StatusSeverity
{
    Neutral = 0,
    Positive = 1,
    Caution = 2,
    Critical = 3,
    Info = 4,
}

public static class StatusSeverityMap
{
    public static StatusSeverity ToSeverity(this Disposition disposition) => disposition switch
    {
        Disposition.Pass => StatusSeverity.Positive,
        Disposition.Watch => StatusSeverity.Caution,
        Disposition.Fail => StatusSeverity.Critical,
        _ => StatusSeverity.Neutral,
    };

    public static StatusSeverity ToSeverity(this AssetCondition condition) => condition switch
    {
        AssetCondition.Healthy => StatusSeverity.Positive,
        AssetCondition.Watch => StatusSeverity.Caution,
        AssetCondition.Failed => StatusSeverity.Critical,
        _ => StatusSeverity.Neutral,
    };

    public static StatusSeverity ToSeverity(this SyncState state) => state switch
    {
        SyncState.Synced => StatusSeverity.Positive,
        SyncState.Pending => StatusSeverity.Info,
        SyncState.Failed => StatusSeverity.Critical,
        _ => StatusSeverity.Neutral,
    };

    /// <summary>Open work waits on the supervisor, dispatched work is under way, closed work is done.</summary>
    public static StatusSeverity ToSeverity(this ActionStatus status) => status switch
    {
        ActionStatus.Open => StatusSeverity.Caution,
        ActionStatus.Dispatched => StatusSeverity.Info,
        ActionStatus.Closed => StatusSeverity.Positive,
        _ => StatusSeverity.Neutral,
    };

    public static StatusSeverity ToSeverity(this CorrectivePriority priority) => priority switch
    {
        CorrectivePriority.Critical or CorrectivePriority.High => StatusSeverity.Critical,
        CorrectivePriority.Medium => StatusSeverity.Caution,
        _ => StatusSeverity.Neutral,
    };
}
