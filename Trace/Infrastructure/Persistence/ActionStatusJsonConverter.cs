using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trace.Infrastructure.Persistence;

/// <summary>
/// Stores <see cref="ActionStatus"/> by name, and reads the names earlier builds wrote.
/// </summary>
/// <remarks>
/// Before dispatch was built, actions carried Open, Assigned, InProgress or Complete, and the
/// seeded action was Assigned. A plain string-enum converter rejects those names, and the store
/// would then set the whole actions file aside as unreadable. Assigned and InProgress were
/// work handed to someone, which is Dispatched now; Complete is Closed.
/// </remarks>
internal sealed class ActionStatusJsonConverter : JsonConverter<ActionStatus>
{
    public override ActionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) && Enum.IsDefined((ActionStatus)number))
        {
            return (ActionStatus)number;
        }

        var name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return name switch
        {
            _ when Enum.TryParse<ActionStatus>(name, ignoreCase: true, out var status) && Enum.IsDefined(status) => status,
            "Assigned" or "InProgress" => ActionStatus.Dispatched,
            "Complete" => ActionStatus.Closed,
            _ => throw new JsonException($"'{name}' is not a corrective action status."),
        };
    }

    public override void Write(Utf8JsonWriter writer, ActionStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
