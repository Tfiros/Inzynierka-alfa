namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Response;

public record UserDetailsResponse(string ProfileDescription, int Tokens, List<string> Roles);