namespace Trace.Models;

public record AppConfig
{
    public string? Environment { get; init; }

    /// <summary>
    /// The technician this device is issued to. Stamped onto every inspection event so a
    /// reading is always attributable. A real deployment would take this from sign-in.
    /// </summary>
    public string? Technician { get; init; }
}
