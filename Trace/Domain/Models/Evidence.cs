namespace Trace.Domain.Models;

/// <summary>
/// A captured artifact backing an inspection. Photos live as app-local files and are
/// referenced here by URI; the record itself stays small enough to sync as structured data.
/// </summary>
public partial record Evidence(
    string Id,
    EvidenceType Type,
    string LocalUri,
    string Caption,
    DateTimeOffset CreatedAt);
