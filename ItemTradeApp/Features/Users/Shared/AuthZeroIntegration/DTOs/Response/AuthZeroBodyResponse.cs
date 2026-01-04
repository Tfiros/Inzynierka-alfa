using System.Text.Json;
using System.Text.Json.Serialization;

namespace ItemTradeApp.AuthZeroCommunication.Dto.Response;

public sealed class AuthZeroBodyResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;

    [JsonPropertyName("details")]
    public AuthZeroBodyDetailsResponseDto Details { get; set; } = default!;
}

public sealed class AuthZeroBodyDetailsResponseDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    [JsonPropertyName("desciption")]
    public string? Description { get; set; }

    [JsonPropertyName("extra")]
    public string? RawResponse { get; set; }
}