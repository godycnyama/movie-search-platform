using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Infrastructure.Services;

/// <summary>
/// Decodes FastMCP tool results. FastMCP publishes typed tool output as
/// structured content, wrapping non-object return types (lists, unions,
/// primitives) in a single-key <c>{"result": ...}</c> envelope; when structured
/// content is absent the JSON text content block is parsed instead.
/// </summary>
public static class McpToolResults
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static T? Deserialize<T>(CallToolResult result) where T : class =>
        Payload(result) is { } payload ? payload.Deserialize<T>(SerializerOptions) : null;

    public static string ErrorMessage(CallToolResult result) =>
        FirstText(result) is { Length: > 0 } text
            ? text
            : "MCP tool call failed without an error message";

    private static JsonElement? Payload(CallToolResult result)
    {
        var element = result.StructuredContent ?? ParseTextContent(result);

        // Unwrap FastMCP's {"result": ...} envelope around non-object payloads.
        if (element is { ValueKind: JsonValueKind.Object } envelope
            && IsSingleResultProperty(envelope, out var inner))
        {
            element = inner;
        }

        return element is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) }
            ? element
            : null;
    }

    private static bool IsSingleResultProperty(JsonElement envelope, out JsonElement inner)
    {
        inner = default;
        var single = false;

        foreach (var property in envelope.EnumerateObject())
        {
            if (single || !property.NameEquals("result"))
            {
                return false; // More than one property, or not the envelope key.
            }

            inner = property.Value;
            single = true;
        }

        return single;
    }

    private static JsonElement? ParseTextContent(CallToolResult result) =>
        FirstText(result) is { Length: > 0 } text
            ? JsonSerializer.Deserialize<JsonElement>(text)
            : null;

    private static string? FirstText(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
}
