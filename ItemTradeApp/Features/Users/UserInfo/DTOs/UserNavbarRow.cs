namespace ItemTradeApp.Features.Users.UserInfo.DTOs;

public sealed record UserNavbarRow(
    int Id, 
    string Email, 
    int Tokens, 
    int EscrowedTokens, 
    int Experience, 
    string Nickname, 
    string? ImageUrl, 
    List<int> ChatIds 
    );