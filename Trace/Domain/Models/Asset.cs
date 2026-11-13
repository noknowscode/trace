namespace Trace.Domain.Models;

/// <summary>
/// A physical piece of equipment. The asset — not a work order — is TRACE's primary
/// navigation surface, so it carries its inspection points directly.
/// </summary>
public partial record Asset(
    string Id,
    string Name,
    AssetKind Kind,
    string TypeLabel,
    string Site,
    string Area,
    string Manufacturer,
    string Model,
    string SerialNumber,
    DateTimeOffset InstalledOn,
    IImmutableList<InspectionPoint> Points,
    string? ModelUri = null)
{
    /// <summary>Worst disposition across all points. Computed so it can never drift from the points.</summary>
    public AssetCondition Condition => Points.Count == 0
        ? AssetCondition.Unknown
        : Points.Any(p => p.Disposition == Disposition.Fail) ? AssetCondition.Failed
        : Points.Any(p => p.Disposition == Disposition.Watch) ? AssetCondition.Watch
        : Points.All(p => p.Disposition == Disposition.None) ? AssetCondition.Unknown
        : AssetCondition.Healthy;

    public int PointCount => Points.Count;

    public int InspectedPointCount => Points.Count(p => p.Disposition != Disposition.None);

    public InspectionPoint? FindPoint(string pointId) =>
        Points.FirstOrDefault(p => p.Id == pointId);

    /// <summary>Returns a copy of this asset with one point replaced.</summary>
    public Asset WithPoint(InspectionPoint point) => this with
    {
        Points = Points.Select(p => p.Id == point.Id ? point : p).ToImmutableList(),
    };
}
