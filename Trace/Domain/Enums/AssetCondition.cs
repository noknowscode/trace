namespace Trace.Domain.Enums;

/// <summary>
/// Rolled-up condition of an asset, derived from the worst disposition across its
/// inspection points. Never stored — always computed so it cannot drift from the points.
/// </summary>
public enum AssetCondition
{
    Unknown = 0,
    Healthy = 1,
    Watch = 2,
    Failed = 3,
}
