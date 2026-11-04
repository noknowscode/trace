namespace Trace.Domain.Enums;

/// <summary>
/// Where corrective work stands. A supervisor moves it forward from Findings, one step at a
/// time: raised (<see cref="Open"/>), sent to its assignee (<see cref="Dispatched"/>), done
/// (<see cref="Closed"/>).
/// </summary>
public enum ActionStatus
{
    Open = 0,
    Dispatched = 1,
    Closed = 2,
}
