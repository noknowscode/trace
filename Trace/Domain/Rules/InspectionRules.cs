using System.Globalization;

namespace Trace.Domain.Rules;

/// <summary>
/// The completion rules from the TRACE build specification, as pure functions so they can
/// be unit tested without a host, a repository or a view.
/// </summary>
public static class InspectionRules
{
    /// <summary>
    /// Parses a reading the way a field technician types it: the device culture first
    /// (so "7,2" works on a French handset), then invariant as a fallback.
    /// </summary>
    public static bool TryParseReading(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var parsed = double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // TryParse accepts "NaN" and "Infinity" under NumberStyles.Float. Neither is a
        // measurement, both compare false against every range so they report as in-range,
        // and System.Text.Json refuses to write them — which would fail the completion and
        // leave the poisoned value in the in-memory snapshot for the rest of the session.
        if (!parsed || double.IsNaN(value) || double.IsInfinity(value))
        {
            value = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluates a draft against its point and reports the one thing standing between the
    /// technician and a completed inspection.
    /// </summary>
    public static InspectionValidation Validate(InspectionPoint point, InspectionDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.ReadingText))
        {
            return new InspectionValidation(
                CanComplete: false,
                Reading: 0,
                RangeStatus: RangeStatus.Unknown,
                Blocker: InspectionBlocker.ReadingMissing,
                Message: $"Enter a reading in {point.Unit}.");
        }

        if (!TryParseReading(draft.ReadingText, out var reading))
        {
            return new InspectionValidation(
                CanComplete: false,
                Reading: 0,
                RangeStatus: RangeStatus.Unknown,
                Blocker: InspectionBlocker.ReadingNotNumeric,
                Message: "The reading must be a number.");
        }

        var range = point.Evaluate(reading);
        var outOfRange = range is RangeStatus.Below or RangeStatus.Above;

        if (draft.Disposition == Disposition.None)
        {
            // Out of range prompts for an explicit call. It never selects Fail on the
            // technician's behalf — the machine's condition is their judgement to make.
            return new InspectionValidation(
                CanComplete: false,
                Reading: reading,
                RangeStatus: range,
                Blocker: InspectionBlocker.DispositionMissing,
                Message: outOfRange
                    ? $"Outside the expected range of {point.ExpectedRange}. Record Pass, Watch or Fail."
                    : "Record Pass, Watch or Fail to complete this point.");
        }

        if (draft.Disposition == Disposition.Fail && !draft.HasNotes && !draft.HasEvidence)
        {
            return new InspectionValidation(
                CanComplete: false,
                Reading: reading,
                RangeStatus: range,
                Blocker: InspectionBlocker.FailNeedsEvidence,
                Message: "A failed point needs a note or a photo before it can be completed.");
        }

        return new InspectionValidation(
            CanComplete: true,
            Reading: reading,
            RangeStatus: range,
            Blocker: InspectionBlocker.None,
            Message: outOfRange
                ? $"Outside the expected range of {point.ExpectedRange}."
                : $"Within the expected range of {point.ExpectedRange}.");
    }

    /// <summary>
    /// Builds the corrective action a failed point implies, carrying asset, point and
    /// source event across so the technician never re-enters context.
    /// </summary>
    public static CorrectiveAction CreateCorrectiveAction(
        Asset asset,
        InspectionPoint point,
        InspectionEvent sourceEvent,
        string summary,
        CorrectivePriority priority,
        string assignee,
        DateTimeOffset createdAt,
        string id) => new(
            Id: id,
            SourceInspectionEventId: sourceEvent.Id,
            AssetId: asset.Id,
            AssetName: asset.Name,
            PointId: point.Id,
            PointLabel: point.Label,
            Priority: priority,
            Summary: summary.Trim(),
            Assignee: assignee,
            Status: ActionStatus.Open,
            CreatedAt: createdAt);

    /// <summary>
    /// The one step corrective work can take from <paramref name="status"/>: an open action is
    /// dispatched, a dispatched one is closed, and a closed one goes nowhere.
    /// </summary>
    public static ActionStatus? NextStatus(ActionStatus status) => status switch
    {
        ActionStatus.Open => ActionStatus.Dispatched,
        ActionStatus.Dispatched => ActionStatus.Closed,
        _ => null,
    };

    /// <summary>The confirmation shown once corrective work is raised.</summary>
    /// <remarks>An assignee written with a closing abbreviation ("Priya S.") ends the sentence itself.</remarks>
    public static string DescribeRaisedAction(CorrectiveAction action) =>
        $"Corrective action raised for {action.PointLabel} and assigned to {action.Assignee}"
        + (action.Assignee.EndsWith('.') ? string.Empty : ".");

    /// <summary>The default summary offered when raising work from a failed inspection.</summary>
    public static string SuggestActionSummary(InspectionEvent failed) =>
        $"Inspect {failed.PointLabel.ToLowerInvariant()} on {failed.AssetId}";
}
