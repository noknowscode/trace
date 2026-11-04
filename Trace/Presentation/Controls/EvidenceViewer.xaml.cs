namespace Trace.Presentation.Controls;

/// <summary>A recorded photo, full size; the asset page hosts it in a modal layer.</summary>
public sealed partial class EvidenceViewer : UserControl
{
    public EvidenceViewer() => InitializeComponent();

    /// <summary>Puts the keyboard on Close, the one control the viewer has.</summary>
    public void FocusClose() => CloseViewerButton.Focus(FocusState.Programmatic);
}
