using System.Text.Json.Serialization;

internal sealed class Auth0UserResponse
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = null!;
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("nickname")] public string? Nickname { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("app_metadata")] public Auth0AppMetadata? AppMetadata { get; set; }
}

internal sealed class Auth0AppMetadata
{
    [JsonPropertyName("roles")] public List<string>? Roles { get; set; }
}
public sealed class Auth0UserSlim
{
    public string UserId { get; set; } = null!;
    public string? Email { get; set; }
    public string? Nickname { get; set; }
    public string? Name { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
}
