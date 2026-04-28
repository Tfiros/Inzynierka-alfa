using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Filters;

public sealed class GlobalHubExceptionFilter(
    ILogger<GlobalHubExceptionFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var connectionId = invocationContext.Context.ConnectionId;
            var userId = invocationContext.Context.UserIdentifier;
            var method = invocationContext.HubMethodName;

            logger.LogError(
                ex,
                "Unhandled SignalR exception. Method: {Method}, UserId: {UserId}, ConnectionId: {ConnectionId}",
                method,
                userId,
                connectionId);

            throw new HubException("unexpected_hub_error");
        }
    }
}