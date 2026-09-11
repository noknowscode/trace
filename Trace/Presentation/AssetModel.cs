using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// The asset workspace. Navigation hands it the row that was tapped; everything else is a
/// live projection keyed on the asset id, so a reading recorded here moves the list behind it.
/// </summary>
public partial record AssetModel
{
    private readonly AssetSummary _summary;
    private readonly IAssetRepository _assets;

    public AssetModel(AssetSummary summary, IAssetRepository assets)
    {
        _summary = summary;
        _assets = assets;
    }

    public string AssetId => _summary.AssetId;

    public string Designation => _summary.Designation;

    public string Location => _summary.Location;

    public IFeed<Asset> Asset => Feed.AsyncEnumerable(ct => _assets.Watch(_summary.AssetId, ct));
}
