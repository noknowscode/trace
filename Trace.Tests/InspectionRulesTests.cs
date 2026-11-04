using System.Globalization;

namespace Trace.Tests;

/// <summary>
/// The completion rules from UNO-BUILD-SPEC, tested as pure functions.
/// </summary>
public class InspectionRulesTests
{
    private static InspectionPoint Point() => new(
        Id: "P-204-seal",
        AssetId: "P-204",
        Sequence: 2,
        Label: "Seal housing",
        Guidance: "Measure at the seal face.",
        X: 0.44,
        Y: 0.52,
        ReadingType: "Vibration velocity",
        Unit: "mm/s",
        NormalMin: 0,
        NormalMax: 2.8,
        LatestReading: 7.2,
        Disposition: Disposition.Fail,
        LastInspectedAt: DateTimeOffset.UnixEpoch);

    private static InspectionDraft Draft(
        string reading,
        Disposition disposition = Disposition.None,
        string notes = "",
        IImmutableList<Evidence>? evidence = null) => new(
            ClientToken: "token",
            AssetId: "P-204",
            PointId: "P-204-seal",
            ReadingText: reading,
            Disposition: disposition,
            Notes: notes,
            Evidence: evidence ?? ImmutableList<Evidence>.Empty);

    private static Evidence Photo() =>
        new("ev-1", EvidenceType.Photo, "file:///tmp/a.png", "Seal face", DateTimeOffset.UnixEpoch);

    [Test]
    public void A_missing_reading_blocks_completion()
    {
        var result = InspectionRules.Validate(Point(), Draft(string.Empty, Disposition.Pass));

        result.CanComplete.Should().BeFalse();
        result.Blocker.Should().Be(InspectionBlocker.ReadingMissing);
        result.Message.Should().Contain("mm/s");
    }

    [Test]
    public void A_non_numeric_reading_blocks_completion()
    {
        var result = InspectionRules.Validate(Point(), Draft("about seven", Disposition.Pass));

        result.CanComplete.Should().BeFalse();
        result.Blocker.Should().Be(InspectionBlocker.ReadingNotNumeric);
    }

    [Test]
    public void A_reading_inside_the_band_is_within_range()
    {
        var result = InspectionRules.Validate(Point(), Draft("1.4", Disposition.Pass));

        result.CanComplete.Should().BeTrue();
        result.RangeStatus.Should().Be(RangeStatus.Within);
        result.IsOutOfRange.Should().BeFalse();
    }

    [Test]
    public void A_reading_above_the_band_is_flagged_but_never_forces_fail()
    {
        var result = InspectionRules.Validate(Point(), Draft("7.2", Disposition.Pass));

        // Out of range is advisory. The technician recorded Pass and Pass is what completes.
        result.CanComplete.Should().BeTrue();
        result.RangeStatus.Should().Be(RangeStatus.Above);
        result.IsOutOfRange.Should().BeTrue();
    }

    [Test]
    public void A_reading_below_the_band_is_flagged_as_below()
    {
        var point = Point() with { NormalMin = 2, NormalMax = 8 };

        InspectionRules.Validate(point, Draft("1.1", Disposition.Watch))
            .RangeStatus.Should().Be(RangeStatus.Below);
    }

    [Test]
    public void An_out_of_range_reading_with_no_disposition_prompts_for_one()
    {
        var result = InspectionRules.Validate(Point(), Draft("7.2"));

        result.CanComplete.Should().BeFalse();
        result.Blocker.Should().Be(InspectionBlocker.DispositionMissing);
        result.Message.Should().Contain("Outside the expected range");
        result.Message.Should().Contain("Pass, Watch or Fail");
    }

    [Test]
    public void An_in_range_reading_still_needs_a_disposition()
    {
        var result = InspectionRules.Validate(Point(), Draft("1.4"));

        result.CanComplete.Should().BeFalse();
        result.Blocker.Should().Be(InspectionBlocker.DispositionMissing);
    }

    [Test]
    public void Fail_without_a_note_or_evidence_is_blocked()
    {
        var result = InspectionRules.Validate(Point(), Draft("7.2", Disposition.Fail));

        result.CanComplete.Should().BeFalse();
        result.Blocker.Should().Be(InspectionBlocker.FailNeedsEvidence);
    }

    [Test]
    public void Fail_with_a_note_is_allowed()
    {
        var result = InspectionRules.Validate(
            Point(),
            Draft("7.2", Disposition.Fail, notes: "Weeping at the seal face."));

        result.CanComplete.Should().BeTrue();
    }

    [Test]
    public void Fail_with_a_photo_and_no_note_is_allowed()
    {
        var result = InspectionRules.Validate(
            Point(),
            Draft("7.2", Disposition.Fail, evidence: ImmutableList.Create(Photo())));

        result.CanComplete.Should().BeTrue();
    }

    [Test]
    public void Whitespace_does_not_count_as_a_note()
    {
        var result = InspectionRules.Validate(Point(), Draft("7.2", Disposition.Fail, notes: "   "));

        result.Blocker.Should().Be(InspectionBlocker.FailNeedsEvidence);
    }

    [Test]
    public void Pass_and_Watch_never_require_evidence()
    {
        InspectionRules.Validate(Point(), Draft("7.2", Disposition.Pass)).CanComplete.Should().BeTrue();
        InspectionRules.Validate(Point(), Draft("7.2", Disposition.Watch)).CanComplete.Should().BeTrue();
    }

    [Test]
    public void A_reading_typed_with_a_comma_decimal_parses_on_a_French_handset()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-CA");
        try
        {
            InspectionRules.TryParseReading("7,2", out var value).Should().BeTrue();
            value.Should().Be(7.2);

            // A dot-typed reading must still work on the same device.
            InspectionRules.TryParseReading("7.2", out var dotted).Should().BeTrue();
            dotted.Should().Be(7.2);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void The_range_boundaries_are_inclusive()
    {
        var point = Point();

        point.Evaluate(point.NormalMin).Should().Be(RangeStatus.Within);
        point.Evaluate(point.NormalMax).Should().Be(RangeStatus.Within);
    }
}
