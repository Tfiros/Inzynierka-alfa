namespace ItemTradeApp.Features.Offers;

public sealed class OfferExpirationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OfferExpirationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var offerExpirationService = scope.ServiceProvider.GetRequiredService<IOfferExpirationService>();
                var expiredCount = await offerExpirationService.ExpireOverdueOffersAsync(ct);
                logger.LogInformation("Expired {Count} offers", expiredCount);

            }catch (Exception e) when(e is not OperationCanceledException)
            {
                logger.LogError(e, "Offer Expiration Job Failed.");
            }
            
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;
            
            await Task.Delay(delay, ct);
        }
    }
}