namespace Trace.Domain.Enums;

/// <summary>
/// The technician's judgement on an inspection point. <see cref="None"/> means the point
/// has not been dispositioned yet and is not a valid completion value.
/// </summary>
public enum Disposition
{
    None = 0,
    Pass = 1,
    Watch = 2,
    Fail = 3,
}
