namespace Trace.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        Loaded += RealizeBothNavigationBars;
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
    private void RealizeBothNavigationBars(object sender, RoutedEventArgs e)
    {
        Loaded -= RealizeBothNavigationBars;

        foreach (var bar in new FrameworkElement[] { RailHost, BottomNavigation })
        {
            if (bar.Visibility == Visibility.Visible)
            {
                continue;
            }

            // Shown for one synchronous layout pass, never rendered: the responsive value is
            // restored before this handler returns.
            bar.Visibility = Visibility.Visible;
            bar.UpdateLayout();
            bar.Visibility = Visibility.Collapsed;
        }
    }
}
