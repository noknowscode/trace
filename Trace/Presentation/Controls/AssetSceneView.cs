using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Trace.Domain.Enums;
using Trace.Presentation.Scene3D;
using Windows.Foundation;

namespace Trace.Presentation.Controls;

/// <summary>
/// The machine itself: a real 3D model of the asset, drawn with perspective and lighting,
/// that the technician can turn to look at the side they are standing on.
/// </summary>
/// <remarks>
/// Built on <see cref="SKXamlCanvas"/> rather than an OpenGL surface because Android is a
/// Tier 1 target and Uno's GL canvas is desktop and Windows only. The geometry, camera and
/// shading are all real; only the rasteriser is Skia.
///
/// The view also publishes where each inspection point projected to, so the XAML markers
/// laid over it stay glued to their part as the model turns. Those markers are ordinary
/// focusable controls — putting them in the scene would have cost keyboard access.
/// </remarks>
public sealed partial class AssetSceneView : SKXamlCanvas, IAnchorProjector
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(AssetKind),
        typeof(AssetSceneView),
        new PropertyMetadata(AssetKind.CentrifugalPump, OnKindChanged));

    public static readonly DependencyProperty SceneBackgroundProperty = DependencyProperty.Register(
        nameof(SceneBackground), typeof(Brush), typeof(AssetSceneView),
        new PropertyMetadata(null, OnPaletteChanged));

    public static readonly DependencyProperty GroundShadowProperty = DependencyProperty.Register(
        nameof(GroundShadow), typeof(Brush), typeof(AssetSceneView),
        new PropertyMetadata(null, OnPaletteChanged));

    public static readonly DependencyProperty KeyLightProperty = DependencyProperty.Register(
        nameof(KeyLight), typeof(Brush), typeof(AssetSceneView),
        new PropertyMetadata(null, OnPaletteChanged));

    private readonly SceneRenderer _renderer = new();
    private readonly SceneCamera _camera = new();

    private Point _dragOrigin;
    private bool _isDragging;

    public AssetSceneView()
    {
        PaintSurface += OnPaintSurface;

        // Dragging is handled through the pointer events below, so the canvas must not also
        // interpret a drag as a manipulation. IsTapEnabled is deliberately not set: it is
        // unimplemented in Uno and only raises Uno0001.
        ManipulationMode = ManipulationModes.None;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _isDragging = false;
        PointerWheelChanged += OnPointerWheelChanged;

        ActualThemeChanged += (_, _) => Invalidate();
    }

    /// <summary>Raised after a frame, once the projected inspection point positions are current.</summary>
    public event EventHandler? ProjectionsChanged;

    public AssetKind Kind
    {
        get => (AssetKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The surface the machine stands on.</summary>
    public Brush? SceneBackground
    {
        get => (Brush?)GetValue(SceneBackgroundProperty);
        set => SetValue(SceneBackgroundProperty, value);
    }

    /// <summary>Tint of the contact shadow under the machine.</summary>
    public Brush? GroundShadow
    {
        get => (Brush?)GetValue(GroundShadowProperty);
        set => SetValue(GroundShadowProperty, value);
    }

    /// <summary>Colour of the dominant light, so the machine is lit by the room it sits in.</summary>
    public Brush? KeyLight
    {
        get => (Brush?)GetValue(KeyLightProperty);
        set => SetValue(KeyLightProperty, value);
    }

    public bool TryGetAnchor(string id, out AnchorProjection projection) =>
        _renderer.Anchors.TryGetValue(id, out projection);

    /// <summary>Returns the camera to the three-quarter view an asset opens on.</summary>
    public void ResetView()
    {
        _camera.Yaw = 0.62f;
        _camera.Pitch = 0.36f;
        _camera.Zoom = 1f;
        Invalidate();
    }

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AssetSceneView)d;
        view.ResetView();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var model = EquipmentModels.For(Kind);
        var info = e.Info;

        _renderer.Render(
            e.Surface.Canvas,
            new SKRect(0, 0, info.Width, info.Height),
            model,
            _camera,
            ReadPalette());

        ProjectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnPaletteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AssetSceneView)d).Invalidate();

    /// <summary>
    /// Lighting colours come from the app theme so the machine sits in the same room as the
    /// rest of the UI in both light and dark.
    /// </summary>
    /// <remarks>
    /// The three brushes are set from a Style rather than read out of
    /// <see cref="Application"/>.Resources: the application dictionary is fixed to the
    /// application's own theme, so it would keep handing back dark colours after the
    /// technician switched the root to light.
    /// </remarks>
    private ScenePalette ReadPalette() => new(
        Background: Resolve(SceneBackground, new SKColor(0x10, 0x1A, 0x18)),
        Ground: Resolve(GroundShadow, new SKColor(0, 0, 0)).WithAlpha(90),
        KeyLight: Resolve(KeyLight, new SKColor(0xE8, 0xF3, 0xEE)),
        // A cool neutral bounce rather than the brand colour: tinting the fill with Primary
        // turned every machine mint green.
        FillLight: new SKColor(0x7E, 0x8C, 0x99));

    private static SKColor Resolve(Brush? brush, SKColor fallback) =>
        brush is SolidColorBrush solid
            ? new SKColor(solid.Color.R, solid.Color.G, solid.Color.B, solid.Color.A)
            : fallback;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragOrigin = e.GetCurrentPoint(this).Position;
        _isDragging = true;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetCurrentPoint(this).Position;
        var deltaX = (float)(position.X - _dragOrigin.X);
        var deltaY = (float)(position.Y - _dragOrigin.Y);
        _dragOrigin = position;

        // A drag across the view is about half a turn, which keeps a single thumb sweep
        // from spinning the machine past the side the technician wanted.
        _camera.Orbit(deltaX * -0.008f, deltaY * 0.006f);
        Invalidate();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        _camera.Zoom = Math.Clamp(_camera.Zoom * (delta > 0 ? 0.9f : 1.11f), 0.55f, 2.2f);
        Invalidate();
        e.Handled = true;
    }
}

/// <summary>Supplies the screen position of an inspection point after the scene is drawn.</summary>
public interface IAnchorProjector
{
    bool TryGetAnchor(string id, out AnchorProjection projection);

    event EventHandler ProjectionsChanged;
}
