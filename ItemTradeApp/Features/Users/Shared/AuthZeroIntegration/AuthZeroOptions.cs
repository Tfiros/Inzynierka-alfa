namespace ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;

public sealed class AuthZeroOptions
{
    public string Domain { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public string ClientId { get; init; } = default!;
    public string ClientSecret { get; init; } = default!;
    public string Realm { get; init; } = "Username-Password-Authentication";
    public ManagementOptions Management { get; set; } = new();

    public sealed class ManagementOptions
    {
        public string ClientId { get; set; } = default!;
        public string ClientSecret { get; set; } = default!;
        public string? Audience { get; set; }
    }
}
