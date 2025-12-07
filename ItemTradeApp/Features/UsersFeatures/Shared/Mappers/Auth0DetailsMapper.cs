using System.Text.Json;
using ItemTradeApp.AuthZeroCommunication.Dto.ResponseDtos;

namespace ItemTradeApp.AuthZeroCommunication.Mappers;

public static class Auth0DetailsMapper
{
    public static AuthZeroBodyResponse Build(string message, string rawBody)
    {
        return new AuthZeroBodyResponse
        {
            Message = message,
            Details = ParseDetails(rawBody)
        };
    }

    public static AuthZeroBodyDetailsResponseDto ParseDetails(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return new AuthZeroBodyDetailsResponseDto();

        var trimmed = rawBody.Trim();

        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return new AuthZeroBodyDetailsResponseDto
            {
                Text = rawBody
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            string? id = null, email = null, error = null, errorDesc = null;
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("_id", out var idEl))          id = idEl.GetString();
            if (root.TryGetProperty("email", out var emEl))        email = emEl.GetString();
            if (root.TryGetProperty("error", out var erEl))        error = erEl.GetString();
            if (root.TryGetProperty("error_description", out var edEl))
                errorDesc = edEl.GetString();

            foreach (var prop in root.EnumerateObject())
            {
                var name = prop.Name;
                if (name is "_id" or "email" or "error" or "error_description" or "desciption") continue;

                extra[name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Number => prop.Value.ToString(),
                    JsonValueKind.True   => "true",
                    JsonValueKind.False  => "false",
                    JsonValueKind.Null   => "null",
                    _ => prop.Value.ToString()
                };
            }

            return new AuthZeroBodyDetailsResponseDto
            {
                Id = id,
                Email = email,
                Error = error,
                ErrorDescription = errorDesc,
                RawResponse = trimmed
            };
        }
        catch (JsonException)
        {
            return new AuthZeroBodyDetailsResponseDto { Text = rawBody, RawResponse = trimmed};
        }
    }
}
