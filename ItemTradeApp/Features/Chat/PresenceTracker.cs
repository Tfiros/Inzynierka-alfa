using System.Collections.Concurrent;

namespace ItemTradeApp.Features.Chat;

public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, int> _connections = new();

    public bool IsOnline(string auth0UserId)
        => _connections.TryGetValue(auth0UserId, out var c) && c > 0;

    public bool UserConnected(string auth0UserId)
    {
        var count = _connections.AddOrUpdate(auth0UserId, 1, (_, c) => c + 1);
        return count == 1;
    }

    public bool UserDisconnected(string auth0UserId)
    {
        if (!_connections.TryGetValue(auth0UserId, out var c)) return false;

        if (c <= 1)
        {
            _connections.TryRemove(auth0UserId, out _);
            return true;
        }

        _connections[auth0UserId] = c - 1;
        return false;
    }
}