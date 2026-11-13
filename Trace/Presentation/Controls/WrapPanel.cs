using Windows.Foundation;

namespace Trace.Presentation.Controls;

/// <summary>
/// Lays children out in a row and starts a new line when the next one will not fit.
/// </summary>
/// <remarks>
/// Chip rows appear on the round, on the asset header, in history and in findings, and at a
/// 360–430 px phone width three chips do not fit on one line. Neither WinUI nor the Toolkit
/// ships a wrapping panel, and AutoLayout stacks without wrapping, so this is the smallest
/// thing that keeps those rows readable without horizontal scrolling.
/// </remarks>
public sealed partial class WrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing), typeof(double), typeof(WrapPanel),
        new PropertyMetadata(8d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing), typeof(double), typeof(WrapPanel),
        new PropertyMetadata(8d, OnLayoutPropertyChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var limit = double.IsInfinity(availableSize.Width) ? double.MaxValue : availableSize.Width;
        var childBudget = new Size(limit, double.PositiveInfinity);

        double lineWidth = 0, lineHeight = 0, totalWidth = 0, totalHeight = 0;

        foreach (var child in Children)
        {
            child.Measure(childBudget);

            // A collapsed chip takes no room and no gap either.
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desired = child.DesiredSize;

            var gap = lineWidth > 0 ? HorizontalSpacing : 0;

            if (lineWidth + gap + desired.Width > limit && lineWidth > 0)
            {
                totalWidth = Math.Max(totalWidth, lineWidth);
                totalHeight += lineHeight + VerticalSpacing;
                lineWidth = desired.Width;
                lineHeight = desired.Height;
                continue;
            }

            lineWidth += gap + desired.Width;
            lineHeight = Math.Max(lineHeight, desired.Height);
        }

        totalWidth = Math.Max(totalWidth, lineWidth);
        totalHeight += lineHeight;

        return new Size(totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, lineHeight = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default);
                continue;
            }

            var desired = child.DesiredSize;
            var gap = x > 0 ? HorizontalSpacing : 0;

            if (x + gap + desired.Width > finalSize.Width && x > 0)
            {
                x = 0;
                y += lineHeight + VerticalSpacing;
                lineHeight = 0;
                gap = 0;
            }

            child.Arrange(new Rect(x + gap, y, desired.Width, desired.Height));
            x += gap + desired.Width;
            lineHeight = Math.Max(lineHeight, desired.Height);
        }

        return new Size(finalSize.Width, y + lineHeight);
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((WrapPanel)d).InvalidateMeasure();
}
