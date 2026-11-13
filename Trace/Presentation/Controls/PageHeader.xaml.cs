using Microsoft.UI.Xaml.Automation;

﻿namespace Trace.Presentation.Controls;

/// <summary>
/// The common heading for every destination: where you are, what it is, and the controls
/// that belong to the whole screen.
/// </summary>
public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty EyebrowProperty = DependencyProperty.Register(
        nameof(Eyebrow), typeof(string), typeof(PageHeader),
        new PropertyMetadata(string.Empty, (d, e) => ((PageHeader)d).HasEyebrow = Show(e.NewValue)));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader),
        new PropertyMetadata(string.Empty, (d, e) => AutomationProperties.SetName(d, e.NewValue as string ?? string.Empty)));

    public static readonly DependencyProperty SupportingProperty = DependencyProperty.Register(
        nameof(Supporting), typeof(string), typeof(PageHeader),
        new PropertyMetadata(string.Empty, (d, e) => ((PageHeader)d).HasSupporting = Show(e.NewValue)));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    // Presence flags are dependency properties of their own rather than computed getters:
    // the inner bindings resolve during InitializeComponent, before the outer attributes are
    // assigned, so a plain getter would latch on to the empty default and never recover.
    public static readonly DependencyProperty HasEyebrowProperty = DependencyProperty.Register(
        nameof(HasEyebrow), typeof(Visibility), typeof(PageHeader),
        new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty HasSupportingProperty = DependencyProperty.Register(
        nameof(HasSupporting), typeof(Visibility), typeof(PageHeader),
        new PropertyMetadata(Visibility.Collapsed));

    public PageHeader() => InitializeComponent();

    public string Eyebrow
    {
        get => (string)GetValue(EyebrowProperty);
        set => SetValue(EyebrowProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Supporting
    {
        get => (string)GetValue(SupportingProperty);
        set => SetValue(SupportingProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public Visibility HasEyebrow
    {
        get => (Visibility)GetValue(HasEyebrowProperty);
        set => SetValue(HasEyebrowProperty, value);
    }

    public Visibility HasSupporting
    {
        get => (Visibility)GetValue(HasSupportingProperty);
        set => SetValue(HasSupportingProperty, value);
    }

    private static Visibility Show(object? value) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
}
