namespace Trace.Domain.Enums;

/// <summary>
/// The single reason a draft cannot be completed yet. Exactly one blocker is reported at a
/// time so the technician always has one clear thing to fix.
/// </summary>
public enum InspectionBlocker
{
    None = 0,
    ReadingMissing = 1,
    ReadingNotNumeric = 2,
    DispositionMissing = 3,
    FailNeedsEvidence = 4,
}
