namespace Trace.Presentation.Controls;

public sealed partial class CorrectiveActionSheet : UserControl
{
    public CorrectiveActionSheet() => InitializeComponent();

    /// <summary>Puts the keyboard on the summary, the one field the sheet asks for first.</summary>
    public void FocusSummary() => SummaryBox.Focus(FocusState.Programmatic);
}
