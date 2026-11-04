namespace Trace.Domain.Models;

/// <summary>
/// One asset as it appears in a list, and the payload carried when navigating to it.
/// Deliberately flat: the join across assignment, asset, points and findings happens once
/// in the repository so rows bind plain values instead of walking nested graphs.
/// Assignment fields are empty when the asset is browsed rather than scheduled.
/// </summary>
public partial record AssetSummary(
    string Id,
    string AssetId,
    string AssetName,
    string TypeLabel,
    string Site,
    string Area,
    string Window,
    string Reason,
    DateTimeOffset? DueAt,
    int PointsTotal,
    int PointsInspected,
    AssetCondition Condition,
    int OpenFindings)
{
    public string Designation => $"{AssetId} · {TypeLabel}";

    public string Location => $"{Area} · {Site}";

    public string Progress => $"{PointsInspected} of {PointsTotal} points inspected";

    public bool IsScheduled => DueAt is not null;

    public string DueLabel => DueAt is { } due ? $"Due {due:t}" : string.Empty;

    public bool HasFindings => OpenFindings > 0;

    public string FindingsText => OpenFindings == 1 ? "1 open finding" : $"{OpenFindings} open findings";

    public string ConditionLabel => Condition switch
    {
        AssetCondition.Failed => "Fail",
        AssetCondition.Watch => "Watch",
        AssetCondition.Healthy => "Pass",
        _ => "Not inspected",
    };
}
