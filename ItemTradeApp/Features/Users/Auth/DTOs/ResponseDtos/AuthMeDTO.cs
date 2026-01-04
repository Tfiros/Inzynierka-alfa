namespace ItemTradeApp.Users.Auth.DTOs.ResponseDtos;
public sealed record AuthMeDTO(
    bool IsAuthenticated,
    string? Login,
    List<string> Roles
);
