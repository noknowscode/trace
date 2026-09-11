namespace Trace.Domain.Enums;

/// <summary>
/// Equipment family. Presentation resolves the spatial schematic from this; the domain
/// only needs to know what kind of machine the technician is standing in front of.
/// </summary>
public enum AssetKind
{
    CentrifugalPump = 0,
    AirHandler = 1,
    Compressor = 2,
}
