namespace Trace.Presentation;

public sealed partial class AssetPage : Page
{
    public AssetPage()
    {
        InitializeComponent();

        // A modal layer takes the keyboard when it opens, and Tab cycles inside it (XAML).
        ActionSheetLayer.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FocusWhenShown(ActionSheetLayer, ActionSheetForm.FocusSummary));
        ViewerLayer.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => FocusWhenShown(ViewerLayer, ViewerForm.FocusClose));
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
}
