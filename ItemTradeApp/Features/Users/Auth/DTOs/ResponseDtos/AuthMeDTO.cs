namespace ItemTradeApp.Users.Auth.DTOs.ResponseDtos;
public sealed record AuthMeDTO(
    int userId,
    bool IsAuthenticated,
    string? Login,
    List<string> Roles
);
