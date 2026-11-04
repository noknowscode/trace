using Trace.Infrastructure;

namespace Trace.Presentation;

public sealed partial class MainPage : Page
{
    /// <summary>The workspace of the asset that was last closed, until the next one opens.</summary>
    private AssetViewModel? _closedAsset;

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += RealizeBothNavigationBars;
        AssetRegion.RegisterPropertyChangedCallback(VisibilityProperty, OnAssetRegionVisibilityChanged);
    }

    /// <summary>
    /// Gives every asset that is opened its own workspace.
    /// </summary>
    /// <remarks>
    /// The asset shares the destinations' Visibility region, whose navigator builds a view model
    /// only when the child it shows has none of the mapped type. Its region child is declared
    /// here rather than created per request, so the first asset's model stayed on it: every later
    /// asset, from any list or a tag, showed the first one, and a reading taken there was recorded
    /// against it. Once the asset is hidden its model is taken off the region, which returns to
    /// the state it had at launch, and the next request builds a model from the asset it carries.
    ///
    /// The closed model is disposed only when the next asset is shown: it is usually hidden by its
    /// own Back command, which is still finishing at that moment, and disposing a model cancels
    /// its commands. Until then it is idle, as the one shared model was before.
    ///
    /// Still needed: the navigator's reuse is unchanged in Uno.Extensions 8.0.0-dev.150, the
    /// version this app builds on, and the <c>--trace-route=Switch</c> hook fails without it.
    /// </remarks>
    private void OnAssetRegionVisibilityChanged(DependencyObject sender, DependencyProperty property)
    {
        // xaml-lint: allow codebehind - reads the region's state; the navigator owns the asset's model and offers no hook when it hides it.
        if (AssetRegion.Visibility == Visibility.Visible)
        {
            DisposeClosedAsset();
        }
        else if (AssetRegion.DataContext is AssetViewModel shown)
        {
            AssetRegion.ClearValue(DataContextProperty);
            DisposeClosedAsset();
            _closedAsset = shown;
        }
    }

    private void DisposeClosedAsset()
    {
        if (_closedAsset is { } closed)
        {
            _closedAsset = null;
            _ = DisposeQuietlyAsync(closed);
        }
    }

    /// <summary>
    /// Disposes a closed workspace without holding up the navigation that closed it. Nothing
    /// awaits this, so a failure is reported here rather than lost with the task.
    /// </summary>
    private static async Task DisposeQuietlyAsync(AssetViewModel closed)
    {
        try
        {
            await closed.DisposeAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Report("Disposing a closed asset workspace", ex);
        }
    }

    /// <summary>
    /// Lays out both navigation bars once, at launch, whichever one the width shows.
    /// </summary>
    /// <remarks>
    /// A TabBar announces its selected item the first time its items are laid out, and the
    /// navigator takes that announcement as a request to go there. The bar for the other width
    /// category starts collapsed, so it used to be laid out for the first time when the window
    /// crossed 600 (a phone turned to landscape, a window dragged wider), and pulled the
    /// technician off the asset they were inspecting back to Today. Laid out at launch, while
    /// Today is the destination anyway, the announcement goes nowhere.
    /// </remarks>
    // WORKAROUND(TabBar navigates when first laid out): a bar first laid out after launch navigates to its selected item; see docs/UNO-AUDIT.md.
    // xaml-lint: allow codebehind - see WORKAROUND
    private void RealizeBothNavigationBars(object sender, RoutedEventArgs e)
    {
        Loaded -= RealizeBothNavigationBars;

        foreach (var bar in new FrameworkElement[] { RailHost, BottomNavigation })
        {
            // xaml-lint: allow codebehind - see WORKAROUND
            if (bar.Visibility == Visibility.Visible)
            {
                continue;
            }

            // Shown for one synchronous layout pass, never rendered: the responsive value is
            // restored before this handler returns.
            // xaml-lint: allow codebehind - see WORKAROUND
            bar.Visibility = Visibility.Visible;
            bar.UpdateLayout();
            bar.Visibility = Visibility.Collapsed;
        }
    }
}
