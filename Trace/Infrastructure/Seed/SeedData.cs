using Trace.Infrastructure.Persistence;

namespace Trace.Infrastructure.Seed;

/// <summary>
/// The round a technician walks on first launch. Written to local storage once and then
/// treated as ordinary state, so everything here can be inspected, failed and escalated
/// exactly like data that arrived from a server.
/// </summary>
public static class SeedData
{
    public const string Technician = "Alex R.";

    public static IImmutableList<string> Assignees { get; } = ImmutableList.Create(
        "Mechanical Team",
        "Reliability Team",
        "Alex R.",
        "Priya S.",
        "Sam O.");

    /// <summary>
    /// Builds the round relative to <paramref name="now"/> so the seeded history reads as
    /// recent work. The clock is passed in rather than read here, which keeps seeding on the
    /// same time source as the rest of the app.
    /// </summary>
    public static TraceSnapshot Create(DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

        var pump = CreatePump(now);
        var airHandler = CreateAirHandler(now);
        var compressor = CreateCompressor(now);

        var assignments = ImmutableList.Create(
            new Assignment("asg-p204", "P-204", 1, "Morning round",
                "Vibration follow-up on seal housing", today.AddHours(10)),
            new Assignment("asg-ahu12", "AHU-12", 2, "Morning round",
                "Monthly filter and belt check", today.AddHours(11.5)),
            new Assignment("asg-c07", "C-07", 3, "Afternoon round",
                "Oil level trending low", today.AddHours(14)));

        var events = CreateHistory(now);

        var actions = ImmutableList.Create(
            new CorrectiveAction(
                Id: "ca-1001",
                SourceInspectionEventId: "ev-p204-seal-1",
                AssetId: "P-204",
                AssetName: "Centrifugal Pump",
                PointId: "P-204-seal",
                PointLabel: "Seal housing",
                Priority: CorrectivePriority.High,
                Summary: "Inspect and replace pump seal at next shutdown",
                Assignee: "Mechanical Team",
                Status: ActionStatus.Assigned,
                CreatedAt: now.AddDays(-1).AddMinutes(12)));

        return new TraceSnapshot(
            Assets: ImmutableList.Create(pump, airHandler, compressor),
            Assignments: assignments,
            Events: events,
            Actions: actions,
            Queue: ImmutableList<SyncOperation>.Empty,
            Drafts: ImmutableList<InspectionDraft>.Empty);
    }

    private static Asset CreatePump(DateTimeOffset now) => new(
        Id: "P-204",
        Name: "Centrifugal Pump",
        Kind: AssetKind.CentrifugalPump,
        TypeLabel: "Centrifugal Pump",
        Site: "Water Treatment",
        Area: "Area B",
        Manufacturer: "Grundfos",
        Model: "NK 80-250/270",
        SerialNumber: "GF-88231-04",
        InstalledOn: new DateTimeOffset(2019, 4, 18, 0, 0, 0, now.Offset),
        // The pump is the one asset that ships with a model file; the other two draw the
        // parametric equipment for their kind, which is also the fallback for this one.
        ModelUri: "ms-appx:///Assets/Models/p-204.glb",
        Points: ImmutableList.Create(
            new InspectionPoint(
                Id: "P-204-motor", AssetId: "P-204", Sequence: 1,
                Label: "Motor body",
                Guidance: "Check for audible noise, surface temperature and mount condition.",
                X: 0.24, Y: 0.39,
                ReadingType: "Surface temperature", Unit: "°C",
                NormalMin: 10, NormalMax: 85,
                LatestReading: 71, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-6)),
            new InspectionPoint(
                Id: "P-204-seal", AssetId: "P-204", Sequence: 2,
                Label: "Seal housing",
                Guidance: "Abnormal vibration recorded on the last two rounds. Measure at the seal face.",
                X: 0.67, Y: 0.53,
                ReadingType: "Vibration velocity", Unit: "mm/s",
                NormalMin: 0, NormalMax: 2.8,
                LatestReading: 7.2, Disposition: Disposition.Fail,
                LastInspectedAt: now.AddDays(-1)),
            new InspectionPoint(
                Id: "P-204-coupling", AssetId: "P-204", Sequence: 3,
                Label: "Coupling",
                Guidance: "Inspect alignment witness marks and guard condition.",
                X: 0.425, Y: 0.47,
                ReadingType: "Alignment offset", Unit: "mm",
                NormalMin: 0, NormalMax: 1.5,
                LatestReading: 1.9, Disposition: Disposition.Watch,
                LastInspectedAt: now.AddDays(-1)),
            new InspectionPoint(
                Id: "P-204-bearing", AssetId: "P-204", Sequence: 4,
                Label: "Drive-end bearing",
                Guidance: "Record bearing temperature after fifteen minutes of running.",
                X: 0.5625, Y: 0.47,
                ReadingType: "Bearing temperature", Unit: "°C",
                NormalMin: 10, NormalMax: 78,
                LatestReading: 64, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-6))));

    private static Asset CreateAirHandler(DateTimeOffset now) => new(
        Id: "AHU-12",
        Name: "Air Handler",
        Kind: AssetKind.AirHandler,
        TypeLabel: "Air Handling Unit",
        Site: "North Plant",
        Area: "Roof",
        Manufacturer: "Trane",
        Model: "M-Series CSAA012",
        SerialNumber: "TR-40119-12",
        InstalledOn: new DateTimeOffset(2021, 9, 2, 0, 0, 0, now.Offset),
        Points: ImmutableList.Create(
            new InspectionPoint(
                Id: "AHU-12-filter", AssetId: "AHU-12", Sequence: 1,
                Label: "Filter bank",
                Guidance: "Read differential pressure across the filter section with the fan running.",
                X: 0.19, Y: 0.51,
                ReadingType: "Differential pressure", Unit: "inH₂O",
                NormalMin: 0.2, NormalMax: 1,
                LatestReading: 0.6, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-28)),
            new InspectionPoint(
                Id: "AHU-12-coil", AssetId: "AHU-12", Sequence: 2,
                Label: "Cooling coil",
                Guidance: "Measure air-side temperature drop across the coil.",
                X: 0.41, Y: 0.51,
                ReadingType: "Air temperature drop", Unit: "°C",
                NormalMin: 5, NormalMax: 14,
                LatestReading: 9.4, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-28)),
            new InspectionPoint(
                Id: "AHU-12-fan", AssetId: "AHU-12", Sequence: 3,
                Label: "Supply fan bearing",
                Guidance: "Infrared reading on the drive-side bearing housing.",
                X: 0.74, Y: 0.42,
                ReadingType: "Bearing temperature", Unit: "°C",
                NormalMin: 10, NormalMax: 70,
                LatestReading: 52, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-28)),
            new InspectionPoint(
                Id: "AHU-12-belt", AssetId: "AHU-12", Sequence: 4,
                Label: "Drive belt",
                Guidance: "Deflection at mid-span under standard belt tension gauge load.",
                X: 0.74, Y: 0.74,
                ReadingType: "Belt deflection", Unit: "mm",
                NormalMin: 6, NormalMax: 12,
                LatestReading: 13.5, Disposition: Disposition.Watch,
                LastInspectedAt: now.AddDays(-28))));

    private static Asset CreateCompressor(DateTimeOffset now) => new(
        Id: "C-07",
        Name: "Rotary Screw Compressor",
        Kind: AssetKind.Compressor,
        TypeLabel: "Air Compressor",
        Site: "Packaging",
        Area: "Line 3",
        Manufacturer: "Atlas Copco",
        Model: "GA 30 VSD+",
        SerialNumber: "AC-77410-07",
        InstalledOn: new DateTimeOffset(2020, 11, 23, 0, 0, 0, now.Offset),
        Points: ImmutableList.Create(
            new InspectionPoint(
                Id: "C-07-motor", AssetId: "C-07", Sequence: 1,
                Label: "Motor winding",
                Guidance: "Read winding temperature from the controller after load stabilises.",
                X: 0.23, Y: 0.44,
                ReadingType: "Winding temperature", Unit: "°C",
                NormalMin: 20, NormalMax: 105,
                LatestReading: 88, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-9)),
            new InspectionPoint(
                Id: "C-07-discharge", AssetId: "C-07", Sequence: 2,
                Label: "Discharge pressure",
                Guidance: "Read the discharge gauge at the airend outlet.",
                X: 0.46, Y: 0.36,
                ReadingType: "Line pressure", Unit: "bar",
                NormalMin: 6.5, NormalMax: 8.5,
                LatestReading: 7.4, Disposition: Disposition.Pass,
                LastInspectedAt: now.AddDays(-9)),
            new InspectionPoint(
                Id: "C-07-oil", AssetId: "C-07", Sequence: 3,
                Label: "Oil separator level",
                Guidance: "Sight glass reading with the machine stopped and settled.",
                X: 0.67, Y: 0.58,
                ReadingType: "Oil level", Unit: "%",
                NormalMin: 45, NormalMax: 90,
                LatestReading: 41, Disposition: Disposition.Watch,
                LastInspectedAt: now.AddDays(-9)),
            new InspectionPoint(
                Id: "C-07-drain", AssetId: "C-07", Sequence: 4,
                Label: "Receiver auto-drain",
                Guidance: "Count drain cycles over one hour of normal duty.",
                X: 0.85, Y: 0.67,
                ReadingType: "Drain cycles", Unit: "per hour",
                NormalMin: 1, NormalMax: 8,
                LatestReading: null, Disposition: Disposition.None,
                LastInspectedAt: null)));

    private static IImmutableList<InspectionEvent> CreateHistory(DateTimeOffset now) =>
        ImmutableList.Create(
            Historic("ev-p204-seal-1", "P-204", "P-204-seal", "Seal housing",
                now.AddDays(-1), 7.2, "mm/s", RangeStatus.Above, Disposition.Fail,
                "Vibration up sharply since last round. Housing warm to the touch and weeping at the seal face."),
            Historic("ev-p204-coupling-1", "P-204", "P-204-coupling", "Coupling",
                now.AddDays(-1).AddMinutes(-6), 1.9, "mm", RangeStatus.Above, Disposition.Watch,
                "Witness marks show slight offset. Re-check after the seal work."),
            Historic("ev-p204-motor-1", "P-204", "P-204-motor", "Motor body",
                now.AddDays(-6), 71, "°C", RangeStatus.Within, Disposition.Pass,
                string.Empty),
            Historic("ev-p204-bearing-1", "P-204", "P-204-bearing", "Drive-end bearing",
                now.AddDays(-6).AddMinutes(-4), 64, "°C", RangeStatus.Within, Disposition.Pass,
                string.Empty),
            Historic("ev-p204-seal-0", "P-204", "P-204-seal", "Seal housing",
                now.AddDays(-14), 4.6, "mm/s", RangeStatus.Above, Disposition.Watch,
                "Reading climbing. Flagged for follow-up on the next round."),
            Historic("ev-ahu12-belt-1", "AHU-12", "AHU-12-belt", "Drive belt",
                now.AddDays(-28), 13.5, "mm", RangeStatus.Above, Disposition.Watch,
                "Belt deflection past limit. Tension at next planned stop."),
            Historic("ev-ahu12-filter-1", "AHU-12", "AHU-12-filter", "Filter bank",
                now.AddDays(-28).AddMinutes(-8), 0.6, "inH₂O", RangeStatus.Within, Disposition.Pass,
                string.Empty),
            Historic("ev-c07-oil-1", "C-07", "C-07-oil", "Oil separator level",
                now.AddDays(-9), 41, "%", RangeStatus.Below, Disposition.Watch,
                "Level below the band. Top up scheduled with the next service kit."));

    private static InspectionEvent Historic(
        string id,
        string assetId,
        string pointId,
        string pointLabel,
        DateTimeOffset at,
        double reading,
        string unit,
        RangeStatus range,
        Disposition disposition,
        string notes) => new(
            Id: id,
            ClientToken: id,
            AssetId: assetId,
            PointId: pointId,
            PointLabel: pointLabel,
            Timestamp: at,
            Technician: Technician,
            Reading: reading,
            Unit: unit,
            RangeStatus: range,
            Disposition: disposition,
            Notes: notes,
            Evidence: ImmutableList<Evidence>.Empty,
            SyncState: SyncState.Synced);
}
