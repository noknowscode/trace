using Microsoft.Extensions.Options;
using Trace.Domain.Enums;
using Trace.Domain.Models;
using Trace.Domain.Rules;
using Trace.Infrastructure.Persistence;
using Trace.Services;

namespace Trace.Infrastructure.Repositories;

/// <summary>
/// Applies the completion rules and commits the result.
/// </summary>
/// <remarks>
/// Every write happens inside a single store mutation, and the store commits a mutation whole
/// (through its journal, see <see cref="StoreCommit"/>), so an inspection can never exist
/// without the point move and the outbox entry that belong with it, not even after a crash
/// halfway through the write. Offline, the local commit is the whole of the transaction.
/// </remarks>
public sealed class InspectionWorkflow : IInspectionWorkflow
{
    private readonly ITraceStore _store;
    private readonly TimeProvider _time;
    private readonly string _technician;
    private readonly ILogger<InspectionWorkflow> _log;

    public InspectionWorkflow(
        ITraceStore store,
        TimeProvider time,
        IOptions<AppConfig> config,
        ILogger<InspectionWorkflow> log)
    {
        _store = store;
        _time = time;
        _log = log;
        _technician = string.IsNullOrWhiteSpace(config.Value.Technician)
            ? "Technician"
            : config.Value.Technician;
    }

    public async ValueTask<InspectionDraft> StartOrResumeDraftAsync(
        InspectionPoint point,
        CancellationToken ct)
    {
        // The lookup has to happen inside the mutation. Checking first and adding after
        // leaves a window where two openings of the same point each add a draft, and the
        // editor then saves into one row and resumes from another — each with its own
        // client token, so the idempotency guard on completion no longer matches either.
        // A new draft starts from the point as stored, so straight after a completion it
        // carries the reading just recorded rather than the one the caller last saw.
        var committed = await _store.MutateAsync(
            s => s.FindDraft(point.Id) is null
                ? s with { Drafts = s.Drafts.Add(InspectionDraft.For(s.FindAsset(point.AssetId)?.FindPoint(point.Id) ?? point)) }
                : s,
            ct);

        return committed.FindDraft(point.Id) ?? InspectionDraft.For(point);
    }

    public async ValueTask SaveDraftAsync(InspectionDraft draft, CancellationToken ct) =>
        await _store.MutateAsync(
            s =>
            {
                // A draft whose token already produced an event is finished work. Saving it
                // again would park that token on the point, and every later Complete there
                // would resolve to the old event as a duplicate and record nothing.
                if (s.Events.Any(e => e.ClientToken == draft.ClientToken))
                {
                    return s;
                }

                var existing = s.Drafts.FirstOrDefault(d => d.PointId == draft.PointId);

                if (existing is null)
                {
                    return s with { Drafts = s.Drafts.Add(draft) };
                }

                // Equal drafts are left alone so an idle editor does not churn the store.
                return existing == draft
                    ? s
                    : s with { Drafts = s.Drafts.Replace(existing, draft) };
            },
            ct);

    public async ValueTask DiscardDraftAsync(string pointId, CancellationToken ct) =>
        await _store.MutateAsync(
            s => s.Drafts.Any(d => d.PointId == pointId)
                ? s with { Drafts = s.Drafts.RemoveAll(d => d.PointId == pointId) }
                : s,
            ct);

    public async ValueTask<CompletionOutcome> CompleteAsync(InspectionDraft draft, CancellationToken ct)
    {
        var snapshot = await _store.GetAsync(ct);

        var asset = snapshot.FindAsset(draft.AssetId);
        var point = asset?.FindPoint(draft.PointId);

        if (asset is null || point is null)
        {
            return CompletionOutcome.Rejected(new InspectionValidation(
                CanComplete: false,
                Reading: 0,
                RangeStatus: RangeStatus.Unknown,
                Blocker: InspectionBlocker.ReadingMissing,
                Message: "This point is no longer available on the device."));
        }

        // A repeated submission of the same draft resolves to the event it already produced.
        // No second event is written, and the caller sees the same outcome as the first tap.
        // A draft still holding that token is cleared, so the point takes new work again.
        if (snapshot.Events.FirstOrDefault(e => e.ClientToken == draft.ClientToken) is { } already)
        {
            await _store.MutateAsync(s => WithoutDraft(s, draft.ClientToken), ct);
            return new CompletionOutcome(true, already, InspectionRules.Validate(point, draft), true, true);
        }

        var validation = InspectionRules.Validate(point, draft);
        if (!validation.CanComplete)
        {
            return CompletionOutcome.Rejected(validation);
        }

        var now = _time.GetLocalNow();
        var completed = new InspectionEvent(
            Id: $"ev-{draft.ClientToken}",
            ClientToken: draft.ClientToken,
            AssetId: asset.Id,
            PointId: point.Id,
            PointLabel: point.Label,
            Timestamp: now,
            Technician: _technician,
            Reading: validation.Reading,
            Unit: point.Unit,
            RangeStatus: validation.RangeStatus,
            Disposition: draft.Disposition,
            Notes: draft.Notes.Trim(),
            Evidence: draft.Evidence,
            SyncState: SyncState.Pending);

        var movedPoint = point with
        {
            LatestReading = validation.Reading,
            Disposition = draft.Disposition,
            LastInspectedAt = now,
        };

        var committed = await _store.MutateAsync(
            s =>
            {
                // Re-check inside the lock: another completion may have landed between the
                // read above and this mutation.
                if (s.Events.Any(e => e.ClientToken == draft.ClientToken))
                {
                    return WithoutDraft(s, draft.ClientToken);
                }

                var current = s.FindAsset(asset.Id);
                if (current is null)
                {
                    return s;
                }

                return s with
                {
                    Assets = s.Assets
                        .Select(a => a.Id == asset.Id ? a.WithPoint(movedPoint) : a)
                        .ToImmutableList(),
                    Events = s.Events.Add(completed),
                    Drafts = s.Drafts.RemoveAll(d => d.PointId == point.Id),
                    Queue = SyncQueue.Enqueue(
                        s.Queue,
                        nameof(InspectionEvent),
                        completed.Id,
                        "create",
                        $"{point.Label} · {completed.ReadingText} · {draft.Disposition}",
                        hasAttachments: draft.Evidence.Any(e => e.Type == EvidenceType.Photo),
                        now),
                };
            },
            ct);

        // The mutation is a no-op when another completion won the race or the asset has gone.
        // Reporting success for a snapshot that never took the event tells the caller its
        // work was queued when nothing was written.
        var stored = committed.Events.FirstOrDefault(e => e.ClientToken == draft.ClientToken);
        if (stored is null)
        {
            _log.LogWarning(
                "Completion for {PointId} was not committed; the asset is no longer on this device.",
                point.Id);

            return new CompletionOutcome(
                false, null, validation, WasQueued: false, WasDuplicate: false,
                Error: "This asset is no longer on the device. The reading was not saved.");
        }

        var duplicate = !ReferenceEquals(stored, completed);

        _log.LogInformation(
            "Inspection completed for {PointId} as {Disposition}.", point.Id, draft.Disposition);

        return new CompletionOutcome(true, stored, validation, WasQueued: !duplicate, WasDuplicate: duplicate);
    }

    private static TraceSnapshot WithoutDraft(TraceSnapshot s, string clientToken) =>
        s.Drafts.Any(d => d.ClientToken == clientToken)
            ? s with { Drafts = s.Drafts.RemoveAll(d => d.ClientToken == clientToken) }
            : s;

    public async ValueTask<CorrectiveActionOutcome> RaiseCorrectiveActionAsync(
        CorrectiveActionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            return CorrectiveActionOutcome.Rejected("A summary is required before the action can be created.");
        }

        var snapshot = await _store.GetAsync(ct);
        var asset = snapshot.FindAsset(request.AssetId);
        var point = asset?.FindPoint(request.PointId);
        var source = snapshot.Events.FirstOrDefault(e => e.Id == request.SourceInspectionEventId);

        if (asset is null || point is null || source is null)
        {
            return CorrectiveActionOutcome.Rejected(
                "The originating inspection is no longer available on the device.");
        }

        // The action inherits its asset, point and source event, so all three must be the
        // same inspection: work raised at one point must not carry another point's event.
        if (source.AssetId != asset.Id || source.PointId != point.Id)
        {
            return CorrectiveActionOutcome.Rejected(
                "That inspection was recorded at another point. Raise the action from the point that failed.");
        }

        if (source.Disposition != Disposition.Fail)
        {
            return CorrectiveActionOutcome.Rejected(
                "Corrective work can only be raised from an inspection recorded as Fail.");
        }

        var now = _time.GetLocalNow();
        var action = InspectionRules.CreateCorrectiveAction(
            asset,
            point,
            source,
            request.Summary,
            request.Priority,
            request.Assignee,
            now,
            $"ca-{Guid.NewGuid():N}");

        await _store.MutateAsync(
            s => s with
            {
                Actions = s.Actions.Add(action),
                Queue = SyncQueue.Enqueue(
                    s.Queue,
                    nameof(CorrectiveAction),
                    action.Id,
                    "create",
                    $"{action.Priority} · {action.Summary}",
                    hasAttachments: false,
                    now),
            },
            ct);

        _log.LogInformation("Corrective action {ActionId} raised from {EventId}.", action.Id, source.Id);

        return new CorrectiveActionOutcome(true, action, null);
    }
}
