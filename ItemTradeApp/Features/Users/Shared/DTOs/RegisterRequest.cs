namespace ItemTradeApp.Features.Users.Shared.DTOs;

public record RegisterRequest(string Email, string Password, DateTime BirthDate, string Username);