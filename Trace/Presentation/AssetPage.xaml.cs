namespace Trace.Presentation;

public sealed partial class AssetPage : Page
{
    /// <summary>How much of the workspace a bottom sheet may cover on a handheld.</summary>
    private const double SheetHeightFraction = 0.62;

    private const double WorkstationWidth = 1000;

    public AssetPage()
    {
        InitializeComponent();

        // A Layout is not part of the visual tree, so it cannot resolve the scene by
        // ElementName. Handing it over here is the whole wiring between the 3D view and the
        // markers laid over it.
        Loaded += (_, _) => MarkerLayout.Anchors = Scene;
    }

    /// <summary>
    /// Caps the inspector to the workspace it sits in.
    /// </summary>
    /// <remarks>
    /// A Stretch alignment grows an element to fill its cell but never shrinks it below its
    /// content, so without a definite maximum the inspector sizes to its own content and
    /// drags the whole workspace taller than the window instead of scrolling inside itself.
    /// The cap has to come from the measured row, which only code can see.
    /// </remarks>
    private void OnWorkspaceResized(object sender, SizeChangedEventArgs e)
    {
        var available = e.NewSize.Height;
        if (available <= 0)
        {
            return;
        }

        InspectorHost.MaxHeight = ActualWidth >= WorkstationWidth
            ? available
            : Math.Max(220, available * SheetHeightFraction);
    }

    private void OnResetView(object sender, RoutedEventArgs e) => Scene.ResetView();
}
