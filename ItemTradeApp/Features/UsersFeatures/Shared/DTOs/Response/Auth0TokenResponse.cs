using System.Text.Json.Serialization;

namespace ItemTradeApp.AuthZeroCommunication.Dto.ResponseDtos;

public sealed class Auth0TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = default!;
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = default!;
}