using Trace.Domain.Enums;

namespace Trace.Presentation.Controls;

/// <summary>
/// One of the three calls a technician can make on a point.
/// </summary>
/// <remarks>
/// The three are laid out as equal, always-visible choices rather than a dropdown or a
/// toggle: recording a condition is the decision the whole app exists for, and it should
/// take one press with a glove on. Kind and chosen state are resolved into a single visual
/// state name because a visual state group cannot see across to another group.
/// </remarks>
public sealed partial class DispositionButton : Button
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(Disposition), typeof(DispositionButton),
        new PropertyMetadata(Disposition.Pass, OnVisualStateChanged));

    public static readonly DependencyProperty IsChosenProperty = DependencyProperty.Register(
        nameof(IsChosen), typeof(bool), typeof(DispositionButton),
        new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(DispositionButton),
        new PropertyMetadata(string.Empty));

    public DispositionButton() => Loaded += (_, _) => Apply(false);

    public Disposition Kind
    {
        get => (Disposition)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsChosen
    {
        get => (bool)GetValue(IsChosenProperty);
        set => SetValue(IsChosenProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Apply(false);
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DispositionButton)d).Apply(true);

    private void Apply(bool useTransitions) =>
        VisualStateManager.GoToState(this, $"{Kind}{(IsChosen ? "Chosen" : "Idle")}", useTransitions);
}
