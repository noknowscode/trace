namespace Trace.Presentation;

public sealed partial class AssetPage : Page
{
    /// <summary>How much of the workspace a bottom sheet may cover on a handheld.</summary>
    private const double SheetHeightFraction = 0.86;

    private const double WorkstationWidth = 1000;

    /// <summary>Tallest the equipment view goes, relative to how wide it is.</summary>
    private const double SceneAspectLimit = 0.8;

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

        var isWorkstation = ActualWidth >= WorkstationWidth;

        InspectorHost.MaxHeight = isWorkstation
            ? available
            : Math.Max(220, available * SheetHeightFraction);

        // On a handheld the workspace column is narrow and tall, and a star row would give
        // the equipment view all of it: a portrait box around a machine that is wider than
        // it is tall. Sizing the view to a landscape shape and letting the row collapse to
        // it keeps the machine as large as it can usefully be and brings the point list up
        // directly underneath rather than stranding it at the bottom of the window.
        if (isWorkstation)
        {
            SceneRow.Height = new GridLength(1, GridUnitType.Star);
            SceneCard.Height = double.NaN;
        }
        else
        {
            SceneRow.Height = GridLength.Auto;
            SceneCard.Height = Math.Max(SceneCard.MinHeight, e.NewSize.Width * SceneAspectLimit);
        }
    }

    private void OnResetView(object sender, RoutedEventArgs e) => Scene.ResetView();
}
