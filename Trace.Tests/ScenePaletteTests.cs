using SkiaSharp;
using Trace.Presentation.Scene3D;

namespace Trace.Tests;

/// <summary>
/// The equipment view takes its lighting and its ground shadow from theme brushes, and a brush
/// carries its strength as Opacity rather than in the colour's alpha. The renderer only sees an
/// SKColor, so the conversion is where "black at 35 %" either survives or turns into a solid
/// black blot under the machine.
/// </summary>
[TestFixture]
public class ScenePaletteTests
{
    [Test]
    public void A_brush_opacity_becomes_the_colour_alpha()
    {
        var shadow = ScenePalette.FromColor(255, 0, 0, 0, 0.35);

        shadow.Should().Be(new SKColor(0, 0, 0, 89));
    }

    [Test]
    public void Opacity_scales_an_alpha_the_colour_already_has()
    {
        var colour = ScenePalette.FromColor(128, 0x7E, 0x8C, 0x99, 0.5);

        colour.Should().Be(new SKColor(0x7E, 0x8C, 0x99, 64));
    }

    [Test]
    public void A_fully_opaque_brush_keeps_its_colour()
    {
        var keyLight = ScenePalette.FromColor(255, 0xE8, 0xF3, 0xEE, 1);

        keyLight.Should().Be(new SKColor(0xE8, 0xF3, 0xEE));
    }

    [TestCase(-0.5, (byte)0)]
    [TestCase(1.5, (byte)255)]
    public void Opacity_outside_the_unit_range_is_clamped(double opacity, byte alpha)
    {
        ScenePalette.FromColor(255, 10, 20, 30, opacity).Alpha.Should().Be(alpha);
    }
}
