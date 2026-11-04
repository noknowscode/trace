namespace Trace.Presentation;

/// <summary>
/// The words the screens use for domain values: button labels, screen-reader names, Sync row
/// labels and the sentences that confirm or suggest. The domain says what is true (an action's
/// next status, an operation's entity); this says it to the technician.
/// </summary>
internal static class ScreenText
{
    /// <summary>The supervisor's next step on an action, as its button reads: Dispatch, then Close.</summary>
    public static string NextStep(CorrectiveAction action) => action.NextStatus switch
    {
        ActionStatus.Dispatched => "Dispatch",
        ActionStatus.Closed => "Close",
        _ => string.Empty,
    };

    /// <summary>The next step with what it applies to, for a screen reader.</summary>
    public static string NextStepName(CorrectiveAction action) => $"{NextStep(action)} {action.Summary}";

    /// <summary>What kind of change a queued operation is, in the words the Sync screen uses.</summary>
    public static string SyncKind(SyncOperation operation) => (operation.EntityType, operation.Operation) switch
    {
        (nameof(InspectionEvent), _) => "Inspection",
        (nameof(CorrectiveAction), "update") => "Action status",
        (nameof(CorrectiveAction), _) => "Corrective action",
        _ => operation.EntityType,
    };

    /// <summary>The confirmation shown once corrective work is raised.</summary>
    /// <remarks>An assignee written with a closing abbreviation ("Priya S.") ends the sentence itself.</remarks>
    public static string ActionRaised(CorrectiveAction action) =>
        $"Corrective action raised for {action.PointLabel} and assigned to {action.Assignee}"
        + (action.Assignee.EndsWith('.') ? string.Empty : ".");

    /// <summary>The summary offered when raising work from a failed inspection.</summary>
    public static string SuggestedActionSummary(InspectionEvent failed) =>
        $"Inspect {failed.PointLabel.ToLowerInvariant()} on {failed.AssetId}";
}
