namespace Trace.Infrastructure.Repositories;

/// <summary>
/// Moves corrective work on, in one store mutation with its outbox entry, so a status that
/// changed on the device is always queued to reach the server.
/// </summary>
public sealed class ActionDispatch : IActionDispatch
{
    private readonly ITraceStore _store;
    private readonly TimeProvider _time;
    private readonly ILogger<ActionDispatch> _log;

    public ActionDispatch(ITraceStore store, TimeProvider time, ILogger<ActionDispatch> log)
    {
        _store = store;
        _time = time;
        _log = log;
    }

    public async ValueTask<CorrectiveActionOutcome> AdvanceAsync(string actionId, ActionStatus from, CancellationToken ct)
    {
        if (InspectionRules.NextStatus(from) is not { } to)
        {
            return CorrectiveActionOutcome.Rejected("A closed action has no further step.");
        }

        var now = _time.GetLocalNow();
        var wrote = false;
        var committed = await _store.MutateAsync(
            s =>
            {
                // Decided inside the lock, against the action as stored: only an action still at
                // the status the supervisor saw moves, so two taps take one step, not two.
                if (s.Actions.FirstOrDefault(a => a.Id == actionId) is not { } action || action.Status != from)
                {
                    return s;
                }

                wrote = true;
                return s with
                {
                    Actions = s.Actions.Replace(action, action with { Status = to }),
                    Queue = SyncQueue.Enqueue(
                        s.Queue,
                        nameof(CorrectiveAction),
                        action.Id,
                        "update",
                        $"{to} · {action.Summary}",
                        hasAttachments: false,
                        now),
                };
            },
            ct);

        var stored = committed.Actions.FirstOrDefault(a => a.Id == actionId);
        if (stored is null)
        {
            return CorrectiveActionOutcome.Rejected("This action is no longer on the device.");
        }

        if (wrote)
        {
            _log.LogInformation("Corrective action {ActionId} moved from {From} to {To}.", actionId, from, to);
            return new CorrectiveActionOutcome(true, stored, null);
        }

        // Nothing written: the step was already taken (a second tap), or the action is
        // somewhere this request did not expect.
        return stored.Status == to
            ? new CorrectiveActionOutcome(true, stored, null, WasDuplicate: true)
            : CorrectiveActionOutcome.Rejected($"This action is {stored.Status} now, so it cannot be moved from {from}.");
    }
}
