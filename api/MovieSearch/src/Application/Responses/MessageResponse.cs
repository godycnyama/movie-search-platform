using System.Text.Json.Serialization;

namespace Application.Responses;

public class MessageResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    public MessageResponse(string message)
    {
        Message = message;
    }
}
