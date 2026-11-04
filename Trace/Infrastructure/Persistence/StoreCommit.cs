namespace Trace.Infrastructure.Persistence;

/// <summary>
/// The sections one mutation changed, as a single document. A null section did not change.
/// </summary>
/// <remarks>
/// Each section is its own file, and one file is written atomically (temp file, then move).
/// A mutation that changes several sections, such as a completion (the event, the point, the
/// outbox entry and the draft), is written to this journal first; once the journal is in
/// place the commit has happened, and a crash before every section reached its own file is
/// finished from the journal on the next launch.
/// </remarks>
internal sealed record StoreCommit(
    ImmutableList<Asset>? Assets,
    ImmutableList<Assignment>? Assignments,
    ImmutableList<InspectionEvent>? Events,
    ImmutableList<CorrectiveAction>? Actions,
    ImmutableList<SyncOperation>? Queue,
    ImmutableList<InspectionDraft>? Drafts)
{
    public int SectionCount =>
        (Assets is null ? 0 : 1)
        + (Assignments is null ? 0 : 1)
        + (Events is null ? 0 : 1)
        + (Actions is null ? 0 : 1)
        + (Queue is null ? 0 : 1)
        + (Drafts is null ? 0 : 1);

    /// <summary>
    /// The sections that differ between two snapshots. Changes are detected by reference: a
    /// section a transform left alone is still the same object.
    /// </summary>
    public static StoreCommit Between(TraceSnapshot previous, TraceSnapshot next) => new(
        Changed(previous.Assets, next.Assets),
        Changed(previous.Assignments, next.Assignments),
        Changed(previous.Events, next.Events),
        Changed(previous.Actions, next.Actions),
        Changed(previous.Queue, next.Queue),
        Changed(previous.Drafts, next.Drafts));

    private static ImmutableList<T>? Changed<T>(IImmutableList<T> previous, IImmutableList<T> next) =>
        ReferenceEquals(previous, next) ? null : next.ToImmutableList();
}
