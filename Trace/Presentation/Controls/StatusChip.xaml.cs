using Microsoft.UI.Xaml.Automation;

﻿namespace Trace.Presentation.Controls;

public sealed partial class StatusChip : UserControl
{
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(StatusSeverity),
        typeof(StatusChip),
        new PropertyMetadata(StatusSeverity.Neutral, OnSeverityChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusChip),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public StatusChip()
    {
        InitializeComponent();

        // A control that has never been in the tree does not apply visual states, so the
        // first paint is driven from Loaded as well as from the property.
        Loaded += (_, _) => Apply();

        // Visual state setters are applied once and are not re-evaluated when the theme
        // changes, so the state has to be re-entered or the chip keeps the other theme's
        // brushes.
        ActualThemeChanged += (_, _) => Apply();
    }

    public StatusSeverity Severity
    {
        get => (StatusSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StatusChip)d).Apply();

    /// <summary>A screen reader reads the chip as its word: the glyph and colour carry nothing for it.</summary>
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        AutomationProperties.SetName(d, e.NewValue as string ?? string.Empty);

    private void Apply() => VisualStateManager.GoToState(this, Severity.ToString(), false);
}
