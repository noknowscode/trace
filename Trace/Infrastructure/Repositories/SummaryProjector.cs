using Trace.Domain.Models;
using Trace.Infrastructure.Persistence;

namespace Trace.Infrastructure.Repositories;

/// <summary>
/// Builds the flat list rows shared by Today and Assets, joining an asset with its point
/// progress and its open findings in one pass over the snapshot.
/// </summary>
internal static class SummaryProjector
{
    public static AssetSummary Scheduled(Assignment assignment, Asset asset, TraceSnapshot snapshot) => new(
        Id: assignment.Id,
        AssetId: asset.Id,
        AssetName: asset.Name,
        TypeLabel: asset.TypeLabel,
        Site: asset.Site,
        Area: asset.Area,
        Window: assignment.Window,
        Reason: assignment.Reason,
        DueAt: assignment.DueAt,
        PointsTotal: asset.PointCount,
        PointsInspected: asset.InspectedPointCount,
        Condition: asset.Condition,
        OpenFindings: snapshot.OpenFindingsFor(asset.Id));

    public static AssetSummary Browsed(Asset asset, TraceSnapshot snapshot) => new(
        Id: asset.Id,
        AssetId: asset.Id,
        AssetName: asset.Name,
        TypeLabel: asset.TypeLabel,
        Site: asset.Site,
        Area: asset.Area,
        Window: string.Empty,
        Reason: string.Empty,
        DueAt: null,
        PointsTotal: asset.PointCount,
        PointsInspected: asset.InspectedPointCount,
        Condition: asset.Condition,
        OpenFindings: snapshot.OpenFindingsFor(asset.Id));

    public static IImmutableList<AssetSummary> Today(TraceSnapshot snapshot) =>
        snapshot.Assignments
            .OrderBy(a => a.Sequence)
            .Select(assignment => (assignment, asset: snapshot.FindAsset(assignment.AssetId)))
            .Where(pair => pair.asset is not null)
            .Select(pair => Scheduled(pair.assignment, pair.asset!, snapshot))
            .ToImmutableList();

    public static IImmutableList<AssetSummary> AllAssets(TraceSnapshot snapshot) =>
        snapshot.Assets
            .OrderBy(a => a.Id, StringComparer.Ordinal)
            .Select(asset => Browsed(asset, snapshot))
            .ToImmutableList();
}
