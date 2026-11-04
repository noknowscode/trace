namespace Trace.Presentation;

public sealed partial class AssetPage : Page
{
    /// <summary>Tallest the equipment view goes under a sheet, relative to how wide it is.</summary>
    private const double SceneAspectLimit = 0.8;

    /// <summary>The equipment view's floor from the token scale, before a short window lowers it.</summary>
    private readonly double _sceneMinHeight;

    public AssetPage()
    {
        InitializeComponent();
        _sceneMinHeight = SceneCard.MinHeight;

        // A Layout is not part of the visual tree, so it cannot resolve the scene by
        // ElementName. Handing it over here is the whole wiring between the 3D view and the
        // markers laid over it.
        Loaded += (_, _) => MarkerLayout.Anchors = Scene;

        // The inspector docks or becomes a sheet when the width category changes (its Grid
        // column is responsive in XAML). The workspace does not always change size at that
        // moment, so the sizing below also follows the column.
        InspectorHost.RegisterPropertyChangedCallback(Grid.ColumnProperty, (_, _) => ApplyWorkspaceLayout());

        // A modal layer takes the keyboard when it opens, and Tab cycles inside it (XAML).
        ActionSheetLayer.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FocusWhenShown(ActionSheetLayer, ActionSheetForm.FocusSummary));
        ViewerLayer.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FocusWhenShown(ViewerLayer, () => CloseViewerButton.Focus(FocusState.Programmatic)));
    }

    // xaml-lint: allow codebehind - focus management: a modal layer that opens takes keyboard focus, which no binding can move.
    private void FocusWhenShown(UIElement layer, Action focus)
    {
        if (layer.Visibility == Visibility.Visible)
        {
            // After this layout pass: an element that is not laid out yet cannot take focus.
            DispatcherQueue.TryEnqueue(() => focus());
        }
    }

    /// <summary>
    /// Pins the page frame to the visible height. The keyboard-aware SafeArea scrolls its
    /// content, so without a definite height the star rows inside would be measured against
    /// infinity; with it, the page fills the window exactly and scrolls only by what an
    /// on-screen keyboard covers.
    /// </summary>
    // xaml-lint: allow codebehind - measured layout: the SafeArea's rendered height has no XAML binding that updates.
    private void OnKeyboardAreaResized(object sender, SizeChangedEventArgs e) =>
        PageFrame.Height = e.NewSize.Height;

    // xaml-lint: allow codebehind - measured layout: the inspector cap and the scene height come from the workspace's rendered size.
    private void OnWorkspaceResized(object sender, SizeChangedEventArgs e) => ApplyWorkspaceLayout();

    /// <summary>
    /// Caps the inspector to the workspace it sits in and sizes the equipment view.
    /// </summary>
    /// <remarks>
    /// A Stretch alignment grows an element to fill its cell but never shrinks it below its
    /// content, so without a definite maximum the inspector sizes to its own content and
    /// drags the whole workspace taller than the window instead of scrolling inside itself.
    /// The cap has to come from the measured row, which only code can see.
    /// </remarks>
    private void ApplyWorkspaceLayout()
    {
        var width = WorkspaceHost.ActualWidth;
        var height = WorkspaceHost.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Docked (Wide and up) the inspector has its own column; as a sheet it spans both.
        var isDocked = Grid.GetColumn(InspectorHost) == 1;

        // A sheet may cover the whole workspace: on a short phone the reading, the three
        // calls and Complete point need all of it, and the tabs above stay visible.
        InspectorHost.MaxHeight = height;

        var room = height - PointListPanel.ActualHeight - SceneStack.RowSpacing;

        if (isDocked)
        {
            // A phone in landscape is docked but short: the machine gives up its floor rather
            // than slide under the point list.
            SceneRow.Height = new GridLength(1, GridUnitType.Star);
            SceneCard.Height = double.NaN;
            SceneCard.MinHeight = Math.Clamp(room, 0, _sceneMinHeight);
            return;
        }

        // Under a sheet the workspace column is narrow and often tall, and a star row would
        // give the equipment view all of it: a portrait box around a machine that is wider
        // than it is tall. Sizing the view to a landscape shape, within the height left once
        // the point list is placed, keeps the machine as large as it can usefully be and
        // brings the point list up directly underneath it.
        SceneRow.Height = GridLength.Auto;
        SceneCard.MinHeight = _sceneMinHeight;
        SceneCard.Height = Math.Max(_sceneMinHeight, Math.Min(width * SceneAspectLimit, room));
    }
}
