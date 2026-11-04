using Uno.Extensions.Reactive;

namespace Trace.Presentation;

/// <summary>
/// Binds the asset workspace's parts to their own controls.
/// </summary>
/// <remarks>
/// MVUX generates one view model per model. The workspace composes two models of its own (the
/// point inspector and the corrective-action sheet), and a model's property is passed to the
/// view as the plain record, whose states a binding cannot read. These additions to the
/// generated view model give each part its own generated view model over the very instance the
/// workspace composed, so the page, the inspector and the sheet share one state, and dispose
/// them with the workspace. Each addition opts out of bindable generation: its name ends in
/// "Model", so MVUX would otherwise take it for a model and generate a view model of it.
/// </remarks>
[ReactiveBindable(false)]
public partial class AssetViewModel
{
    private PointInspectorViewModel? _inspector;
    private CorrectiveActionSheetViewModel? _actionSheet;

    /// <summary>The DataContext of the point inspector.</summary>
    public PointInspectorViewModel Inspector => _inspector ??= Owned(PointInspectorViewModel.Over(Model.InspectorModel));

    /// <summary>The DataContext of the corrective-action sheet.</summary>
    public CorrectiveActionSheetViewModel ActionSheet => _actionSheet ??= Owned(CorrectiveActionSheetViewModel.Over(Model.ActionSheetModel));

    private T Owned<T>(T part)
        where T : IAsyncDisposable
    {
        RegisterDisposable(part);
        return part;
    }
}

[ReactiveBindable(false)]
public partial class PointInspectorViewModel
{
    /// <summary>A view model over a point inspector the workspace already composed.</summary>
    internal static PointInspectorViewModel Over(PointInspectorModel model) => new(model);
}

[ReactiveBindable(false)]
public partial class CorrectiveActionSheetViewModel
{
    /// <summary>A view model over a sheet the workspace already composed.</summary>
    internal static CorrectiveActionSheetViewModel Over(CorrectiveActionSheetModel model) => new(model);
}
