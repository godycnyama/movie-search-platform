using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Generic single-message wrapper used for simple success/informational responses.
/// Mirrors the reference API's <c>MessageResponse</c> pattern.
/// </summary>
public class MessageResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    public MessageResponse(string message)
    {
        Message = message;
    }
}
