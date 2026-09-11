using Trace.Domain.Enums;
using Trace.Domain.Models;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// The whole of TRACE's local state at one instant. Every mutation produces a new snapshot,
/// so readers never observe a half-applied change and the sections that did not move keep
/// their references — which is how the store knows what to persist.
/// </summary>
public sealed record TraceSnapshot(
    IImmutableList<Asset> Assets,
    IImmutableList<Assignment> Assignments,
    IImmutableList<InspectionEvent> Events,
    IImmutableList<CorrectiveAction> Actions,
    IImmutableList<SyncOperation> Queue,
    IImmutableList<InspectionDraft> Drafts)
{
    public static TraceSnapshot Empty { get; } = new(
        ImmutableList<Asset>.Empty,
        ImmutableList<Assignment>.Empty,
        ImmutableList<InspectionEvent>.Empty,
        ImmutableList<CorrectiveAction>.Empty,
        ImmutableList<SyncOperation>.Empty,
        ImmutableList<InspectionDraft>.Empty);

    public Asset? FindAsset(string assetId) => Assets.FirstOrDefault(a => a.Id == assetId);

    public InspectionDraft? FindDraft(string pointId) => Drafts.FirstOrDefault(d => d.PointId == pointId);

    public int PendingSyncCount => Queue.Count(o => o.Status is SyncState.Pending or SyncState.Failed);

    public int OpenFindingsFor(string assetId) =>
        Actions.Count(a => a.AssetId == assetId && a.Status != ActionStatus.Complete);
}
