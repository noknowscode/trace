namespace Trace.Domain.Enums;

/// <summary>
/// Where a reading falls relative to its point's expected range. This drives the
/// out-of-range prompt; it never selects a disposition on the technician's behalf.
/// </summary>
public enum RangeStatus
{
    /// <summary>No reading entered, or the text is not a number.</summary>
    Unknown = 0,
    Within = 1,
    Below = 2,
    Above = 3,
}
