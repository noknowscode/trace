namespace Trace.Presentation.Controls;

// xaml-lint: allow builtin - the form, not a sheet surface: the asset page draws the modal layer around it, as it did before the form had its own model (a "!" flyout route would rebuild it per open).
public sealed partial class CorrectiveActionSheet : UserControl
{
    public CorrectiveActionSheet() => InitializeComponent();

    /// <summary>Puts the keyboard on the summary, the one field the sheet asks for first.</summary>
    public void FocusSummary() => SummaryBox.Focus(FocusState.Programmatic);
}
