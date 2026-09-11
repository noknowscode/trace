namespace Trace.Domain.Models;

/// <summary>
/// An inspection point as the spatial view needs it: the point itself plus whether it is the
/// one currently open. Selection is projected into the row rather than resolved by a
/// converter so the marker can bind it directly.
/// </summary>
public partial record PointMarkerView(InspectionPoint Point, bool IsSelected)
{
    public string Id => Point.Id;
}
