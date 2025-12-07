namespace ItemTradeApp.AuthZeroCommunication;

public sealed class Auth0Options
{
    public string Domain { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public string ClientId { get; init; } = default!;
    public string ClientSecret { get; init; } = default!;
    public string Realm { get; init; } = "Username-Password-Authentication";
}
