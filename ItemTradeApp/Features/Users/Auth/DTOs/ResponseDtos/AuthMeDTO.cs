namespace ItemTradeApp.Features.Users.Auth.DTOs.ResponseDtos;
public sealed record AuthMeDTO(
    int UserId,
    bool IsAuthenticated,
    string? Login,
    List<string> Roles
);
