namespace Trace.Services;

/// <summary>
/// Everything that happens at an inspection point, as one transaction: validate, commit the
/// event, move the point, queue the sync, and clear the draft.
/// </summary>
public interface IInspectionWorkflow
{
    /// <summary>Resumes the saved draft for a point, or starts a fresh one.</summary>
    ValueTask<InspectionDraft> StartOrResumeDraftAsync(InspectionPoint point, CancellationToken ct);

    ValueTask SaveDraftAsync(InspectionDraft draft, CancellationToken ct);

    ValueTask DiscardDraftAsync(string pointId, CancellationToken ct);

    ValueTask<CompletionOutcome> CompleteAsync(InspectionDraft draft, CancellationToken ct);

    ValueTask<CorrectiveActionOutcome> RaiseCorrectiveActionAsync(
        CorrectiveActionRequest request,
        CancellationToken ct);
}

public sealed record CompletionOutcome(
    bool Succeeded,
    InspectionEvent? Event,
    InspectionValidation Validation,
    bool WasQueued,
    bool WasDuplicate,
    string? Error = null)
{
    public static CompletionOutcome Rejected(InspectionValidation validation) =>
        new(false, null, validation, false, false);
}

public sealed record CorrectiveActionRequest(
    string AssetId,
    string PointId,
    string SourceInspectionEventId,
    string Summary,
    CorrectivePriority Priority,
    string Assignee);

public sealed record CorrectiveActionOutcome(bool Succeeded, CorrectiveAction? Action, string? Error)
{
    public static CorrectiveActionOutcome Rejected(string error) => new(false, null, error);
}
