namespace Trace.Presentation;

/// <summary>
/// A recorded photo shown full size over the asset workspace, opened from History or from the
/// point inspector.
/// </summary>
/// <remarks>
/// A part of its own, like the corrective-action sheet, so the inspector opens it through its
/// own model rather than reaching the page's.
/// </remarks>
public partial record EvidenceViewerModel
{
    /// <summary>The photo being shown.</summary>
    public IState<Evidence> Viewed => State<Evidence>.Empty(this);

    public IState<bool> IsOpen => State.Value(this, () => false);

    /// <summary>Shows <paramref name="evidence"/> full size.</summary>
    internal async ValueTask OpenAsync(Evidence evidence, CancellationToken ct)
    {
        await Viewed.UpdateAsync(_ => evidence, ct);
        await IsOpen.UpdateAsync(_ => true, ct);
    }

    public ValueTask Close(CancellationToken ct) => IsOpen.UpdateAsync(_ => false, ct);
}
