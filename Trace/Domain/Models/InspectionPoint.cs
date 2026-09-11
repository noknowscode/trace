using Trace.Domain.Enums;

namespace Trace.Domain.Models;

/// <summary>
/// A named measurement location on an asset, anchored to the equipment view by a
/// normalized (0..1) position so the marker tracks the machine at any canvas size.
/// </summary>
public partial record InspectionPoint(
    string Id,
    string AssetId,
    int Sequence,
    string Label,
    string Guidance,
    double X,
    double Y,
    string ReadingType,
    string Unit,
    double NormalMin,
    double NormalMax,
    double? LatestReading,
    Disposition Disposition,
    DateTimeOffset? LastInspectedAt)
{
    public string ExpectedRange => $"{Format(NormalMin)}–{Format(NormalMax)} {Unit}";

    public string LatestReadingText => LatestReading is { } value ? Format(value) : "—";

    public bool HasReading => LatestReading is not null;

    /// <summary>Where the stored latest reading sits relative to the expected range.</summary>
    public RangeStatus LatestRangeStatus =>
        LatestReading is { } value ? Evaluate(value) : RangeStatus.Unknown;

    public RangeStatus Evaluate(double reading) =>
        reading < NormalMin ? RangeStatus.Below
        : reading > NormalMax ? RangeStatus.Above
        : RangeStatus.Within;

    private static string Format(double value) =>
        value == Math.Floor(value) ? value.ToString("0") : value.ToString("0.##");
}
