namespace Trace.Presentation.Controls;

public sealed partial class PointInspector : UserControl
{
    private bool? _scrollsAsOne;

    public PointInspector() => InitializeComponent();

    /// <summary>
    /// Pins the reading, the calls and Complete point when the height allows, and scrolls the
    /// whole panel as one when it does not.
    /// </summary>
    /// <remarks>
    /// The pinned layout needs room for its header and footer; the middle may shrink to
    /// nothing, as it does on a 360 x 640 phone. A phone in landscape gives the panel about
    /// 200 px, less than the footer alone, and then the panel scrolls as one. The header's and
    /// footer's natural heights do not depend on the state chosen, and neither does the height
    /// this control is given, so the choice cannot oscillate.
    /// </remarks>
    // xaml-lint: allow codebehind - measured layout: the pinned parts' rendered heights against the height the panel is given.
    private void OnPartResized(object sender, SizeChangedEventArgs e)
    {
        var given = ActualHeight;
        if (given <= 0)
        {
            return;
        }

        var rows = (Grid)WholePanel.Content;
        var pinned = Header.ActualHeight + Footer.DesiredSize.Height + (2 * rows.RowSpacing);
        var scrollsAsOne = given < pinned;

        if (scrollsAsOne != _scrollsAsOne)
        {
            _scrollsAsOne = scrollsAsOne;
            VisualStateManager.GoToState(this, scrollsAsOne ? "ScrollsAsOne" : "Pinned", useTransitions: false);
        }
    }
}
