using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Trace.Presentation.Controls;

/// <summary>
/// Keeps the inspection point markers glued to the machine.
/// </summary>
/// <remarks>
/// Positions come from the 3D scene: each marker carries the id of an anchor on the model,
/// and the layout places it wherever that anchor projected to on the last frame. Turn the
/// machine and the markers travel with their parts, including around the back.
///
/// The markers stay ordinary XAML controls rather than being drawn into the scene so they
/// keep focus, keyboard activation and automation names — a point has to be reachable
/// without a pointer.
/// </remarks>
public sealed partial class SpatialMarkerLayout : NonVirtualizingLayout
{
    private IAnchorProjector? _anchors;

    /// <summary>
    /// The scene that resolves anchor ids. Assigned from code rather than bound, because a
    /// Layout is not in the visual tree and cannot resolve an ElementName.
    /// </summary>
    public IAnchorProjector? Anchors
    {
        get => _anchors;
        set
        {
            if (ReferenceEquals(_anchors, value))
            {
                return;
            }

            if (_anchors is not null)
            {
                _anchors.ProjectionsChanged -= OnProjectionsChanged;
            }

            _anchors = value;

            if (_anchors is not null)
            {
                _anchors.ProjectionsChanged += OnProjectionsChanged;
            }
        }
    }

    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        foreach (var child in context.Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        foreach (var child in context.Children)
        {
            var size = child.DesiredSize;
            var id = SpatialMarkers.GetAnchorId(child);

            if (_anchors is null
                || string.IsNullOrEmpty(id)
                || !_anchors.TryGetAnchor(id, out var projection))
            {
                // Nothing to pin to yet: park it out of sight rather than at the origin,
                // where it would sit in a pile over the machine's centre.
                child.Arrange(new Rect(-1000, -1000, size.Width, size.Height));
                continue;
            }

            var centreX = projection.X * finalSize.Width;
            var centreY = projection.Y * finalSize.Height;

            child.Arrange(new Rect(
                centreX - (size.Width / 2),
                centreY - (size.Height / 2),
                size.Width,
                size.Height));

            // A point on the far side of the machine reads as behind it rather than in
            // front, so the technician can tell which side they are looking at.
            if (child is FrameworkElement element)
            {
                element.Opacity = projection.IsFacingCamera ? 1 : 0.4;
            }
        }

        return finalSize;
    }

    private void OnProjectionsChanged(object? sender, EventArgs e) => InvalidateArrange();
}

/// <summary>Ties a marker to a named anchor on the 3D model.</summary>
public static class SpatialMarkers
{
    public static readonly DependencyProperty AnchorIdProperty = DependencyProperty.RegisterAttached(
        "AnchorId", typeof(string), typeof(SpatialMarkers), new PropertyMetadata(string.Empty));

    public static string GetAnchorId(DependencyObject element) =>
        (string)element.GetValue(AnchorIdProperty);

    public static void SetAnchorId(DependencyObject element, string value) =>
        element.SetValue(AnchorIdProperty, value);
}
