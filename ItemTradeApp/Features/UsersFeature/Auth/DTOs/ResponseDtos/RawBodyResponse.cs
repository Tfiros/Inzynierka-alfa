using System.Text.Json.Serialization;

namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public sealed class RawBodyResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("details")]
    public BodyDetailsResponseDto Details { get; init; } = default!;
}

public sealed class BodyDetailsResponseDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("extra")]
    public Dictionary<string, string>? Extra { get; init; }
}