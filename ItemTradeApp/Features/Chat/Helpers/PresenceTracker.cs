using System.Collections.Concurrent;

namespace ItemTradeApp.Features.Chat.Helpers;

public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, int> _connections = new();

    public bool IsOnline(string auth0UserId)
    {
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        return _connections.TryGetValue(trimmedAuth0UserId, out var c) && c > 0;
    }

    public bool UserConnected(string auth0UserId)
    {
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var count = _connections.AddOrUpdate(trimmedAuth0UserId, 1, (_, c) => c + 1);
        return count == 1;
    }

    public bool UserDisconnected(string auth0UserId)
    {
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;

        while (true)
        {
            if (!_connections.TryGetValue(trimmedAuth0UserId, out var current))
                return false;

            if (current <= 1)
            {
                if (_connections.TryRemove(
                        new KeyValuePair<string, int>(trimmedAuth0UserId, current)))
                {
                    return true;
                }

                continue;
            }

            if (_connections.TryUpdate(trimmedAuth0UserId, current - 1, current))
                return false;
        }
    }
}