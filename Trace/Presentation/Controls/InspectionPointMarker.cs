namespace Trace.Presentation.Controls;

/// <summary>
/// A target pinned to the machine.
/// </summary>
/// <remarks>
/// It derives from Button rather than being drawn into the canvas so a point can be tabbed
/// to, activated from the keyboard, shown as focused and read by a screen reader — none of
/// which comes for free on a drawn surface. Its template is the keyed
/// <c>InspectionPointMarkerStyle</c> in Themes/Markers.xaml, applied at the usage site
/// rather than through a default style key, which keeps resolution unambiguous.
/// </remarks>
public sealed partial class InspectionPointMarker : Button
{
    public static readonly DependencyProperty SequenceProperty = DependencyProperty.Register(
        nameof(Sequence), typeof(int), typeof(InspectionPointMarker), new PropertyMetadata(0));

    public static readonly DependencyProperty DispositionProperty = DependencyProperty.Register(
        nameof(Disposition), typeof(Disposition), typeof(InspectionPointMarker),
        new PropertyMetadata(Disposition.None, OnVisualStateChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(InspectionPointMarker),
        new PropertyMetadata(false, OnVisualStateChanged));

    public InspectionPointMarker()
    {
        Loaded += (_, _) => Apply(false);

        // Visual state setters are applied once and are not re-evaluated when the theme
        // changes, so the state has to be re-entered or the control keeps the other theme's
        // brushes.
        ActualThemeChanged += (_, _) => Apply(false);
    }

    public int Sequence
    {
        get => (int)GetValue(SequenceProperty);
        set => SetValue(SequenceProperty, value);
    }

    public Disposition Disposition
    {
        get => (Disposition)GetValue(DispositionProperty);
        set => SetValue(DispositionProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Apply(false);
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InspectionPointMarker)d).Apply(true);

    private void Apply(bool useTransitions)
    {
        VisualStateManager.GoToState(this, Disposition.ToString(), useTransitions);
        VisualStateManager.GoToState(this, IsSelected ? "Selected" : "Unselected", useTransitions);
    }
}
