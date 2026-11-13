namespace Trace.Services;

/// <summary>
/// The supervisor's side of corrective work: dispatching it to its assignee and closing it.
/// Each step is a local write with its own sync operation, like everything else TRACE records.
/// </summary>
public interface IActionDispatch
{
    /// <summary>
    /// Moves an action one step on from <paramref name="from"/>: Open to Dispatched, Dispatched
    /// to Closed. A repeated request for a step already taken resolves to the action as it is
    /// and writes nothing, so a double tap never skips a step.
    /// </summary>
    ValueTask<CorrectiveActionOutcome> AdvanceAsync(string actionId, ActionStatus from, CancellationToken ct);
}
