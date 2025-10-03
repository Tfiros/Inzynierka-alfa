using System.Text.Json;
using ItemTradeApp.LoginFeature.Dto.ResponseDtos;

namespace ItemTradeApp.LoginFeature.Mappers;

public static class Auth0DetailsMapper
{
    /// <summary>
    /// Buduje RawBodyResponseDto z wiadomością i surowym body z Auth0 (JSON lub text).
    /// Rozpoznaje: _id, email, error, error_description. Resztę wrzuca do 'extra'.
    /// </summary>
    public static RawBodyResponseDto Build(string message, string rawBody)
    {
        return new RawBodyResponseDto
        {
            Message = message,
            Details = ParseDetails(rawBody)
        };
    }

    public static BodyDetailsResponseDto ParseDetails(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return new BodyDetailsResponseDto();

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            string? id = null, email = null, error = null, errorDesc = null;
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("_id", out var idEl))    id = idEl.GetString();
            if (root.TryGetProperty("email", out var emEl))  email = emEl.GetString();
            if (root.TryGetProperty("error", out var erEl))  error = erEl.GetString();
            if (root.TryGetProperty("error_description", out var edEl)) errorDesc = edEl.GetString();

            foreach (var prop in root.EnumerateObject())
            {
                var name = prop.Name;
                if (name is "_id" or "email" or "error" or "error_description") continue;
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

            return new BodyDetailsResponseDto
            {
                Id = id,
                Email = email,
                Error = error,
                ErrorDescription = errorDesc,
                Extra = extra.Count > 0 ? extra : null
            };
        }
        catch (JsonException)
        {
            return new BodyDetailsResponseDto { Text = rawBody };
        }
    }
}