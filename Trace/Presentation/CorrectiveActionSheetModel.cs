namespace Trace.Presentation;

/// <summary>
/// The corrective-action sheet: the work raised from an inspection that failed, with its
/// summary, priority and assignee.
/// </summary>
/// <remarks>
/// The sheet is opened by the point inspector with the event just recorded there, and it raises
/// work from that event only, never from whatever the editor shows by then. The asset, the
/// point and the event are carried across by the workflow, so the form asks for nothing else.
/// </remarks>
public partial record CorrectiveActionSheetModel
{
    private const string DefaultAssignee = "Mechanical Team";

    private readonly IInspectionWorkflow _workflow;
    private readonly ICorrectiveActionRepository _actions;

    public CorrectiveActionSheetModel(IInspectionWorkflow workflow, ICorrectiveActionRepository actions)
    {
        _workflow = workflow;
        _actions = actions;
    }

    /// <summary>The recorded inspection the work is raised from.</summary>
    public IState<InspectionEvent> Source => State<InspectionEvent>.Empty(this);

    public IState<bool> IsOpen => State.Value(this, () => false);

    public IState<string> Summary => State.Value(this, () => string.Empty);

    public IState<string> Priority => State.Value(this, () => nameof(CorrectivePriority.High));

    public IState<string> Assignee => State.Value(this, () => DefaultAssignee);

    public IState<string> Error => State<string>.Empty(this);

    /// <summary>The last action this sheet raised, for the inspector to confirm.</summary>
    public IState<CorrectiveAction> Raised => State<CorrectiveAction>.Empty(this);

    /// <summary>
    /// Mirrors the workflow's own rule so the button is dead until the action can succeed,
    /// the same way Complete point behaves. The workflow still rejects a blank summary:
    /// this is the affordance, not the enforcement.
    /// </summary>
    public IFeed<bool> CanCreate => Summary.Select(summary => !string.IsNullOrWhiteSpace(summary));

    public IFeed<IImmutableList<string>> Assignees =>
        Feed.Async(async ct => await _actions.GetAssigneesAsync(ct));

    public IFeed<IImmutableList<string>> Priorities => Feed.Async(_ => ValueTask.FromResult(
        (IImmutableList<string>)ImmutableList.Create(
            nameof(CorrectivePriority.Critical),
            nameof(CorrectivePriority.High),
            nameof(CorrectivePriority.Medium),
            nameof(CorrectivePriority.Low))));

    /// <summary>
    /// Shows the sheet for <paramref name="source"/>. A summary typed for the same inspection
    /// is kept, so Cancel and reopen loses nothing; a new inspection starts from the suggestion.
    /// </summary>
    internal async ValueTask OpenAsync(InspectionEvent source, string suggestedSummary, CancellationToken ct)
    {
        var current = await Source;
        if (current?.Id != source.Id)
        {
            await Source.UpdateAsync(_ => source, ct);
            await Summary.UpdateAsync(_ => suggestedSummary, ct);
        }

        await Error.UpdateAsync(_ => string.Empty, ct);
        await IsOpen.UpdateAsync(_ => true, ct);
    }

    /// <summary>Forgets the inspection the sheet was raising work from: another point was opened.</summary>
    internal async ValueTask ResetAsync(CancellationToken ct)
    {
        await IsOpen.UpdateAsync(_ => false, ct);
        await Source.UpdateAsync(_ => null, ct);
        await Summary.UpdateAsync(_ => string.Empty, ct);
        await Error.UpdateAsync(_ => string.Empty, ct);
        await Raised.UpdateAsync(_ => null, ct);
    }

    public ValueTask Cancel(CancellationToken ct) => IsOpen.UpdateAsync(_ => false, ct);

    public async ValueTask Create(CancellationToken ct)
    {
        var source = await Source;
        if (source is null || source.Disposition != Disposition.Fail)
        {
            return;
        }

        var priority = Enum.TryParse<CorrectivePriority>(await Priority, out var parsed)
            ? parsed
            : CorrectivePriority.High;

        var request = new CorrectiveActionRequest(
            AssetId: source.AssetId,
            PointId: source.PointId,
            SourceInspectionEventId: source.Id,
            Summary: await Summary ?? string.Empty,
            Priority: priority,
            Assignee: await Assignee ?? DefaultAssignee);

        CorrectiveActionOutcome outcome;
        try
        {
            outcome = await _workflow.RaiseCorrectiveActionAsync(request, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            // The store finishes a half-written commit on its next read, so the action may yet
            // appear. Trying again is safe: an inspection raises one action, never two.
            await Error.UpdateAsync(
                _ => "The action could not be saved on the device. Try Create action again; it is never raised twice. If it keeps failing, restart TRACE.",
                ct);
            return;
        }

        if (!outcome.Succeeded || outcome.Action is not { } raised)
        {
            await Error.UpdateAsync(_ => outcome.Error ?? "The action could not be created.", ct);
            return;
        }

        await IsOpen.UpdateAsync(_ => false, ct);
        await Error.UpdateAsync(_ => string.Empty, ct);
        await Raised.UpdateAsync(_ => raised, ct);
    }
}
