using System.Text.Json.Serialization;
using Trace.Domain.Models;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Source-generated serialization for the persisted documents. Reflection-based
/// serialization is not safe under Android trimming, so every stored shape is declared here.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ImmutableList<Asset>))]
[JsonSerializable(typeof(ImmutableList<Assignment>))]
[JsonSerializable(typeof(ImmutableList<InspectionEvent>))]
[JsonSerializable(typeof(ImmutableList<CorrectiveAction>))]
[JsonSerializable(typeof(ImmutableList<SyncOperation>))]
[JsonSerializable(typeof(ImmutableList<InspectionDraft>))]
internal sealed partial class TraceJsonContext : JsonSerializerContext;
