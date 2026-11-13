namespace Trace.Domain.Rules;

/// <summary>
/// The outcome of validating a draft against its point. <see cref="RangeStatus"/> is
/// reported independently of <see cref="CanComplete"/>: an out-of-range reading is advisory
/// information the technician must judge, never an automatic failure.
/// </summary>
public partial record InspectionValidation(
    bool CanComplete,
    double Reading,
    RangeStatus RangeStatus,
    InspectionBlocker Blocker,
    string Message)
{
    public bool IsOutOfRange => RangeStatus is RangeStatus.Below or RangeStatus.Above;

    public bool HasBlocker => Blocker != InspectionBlocker.None;
}
