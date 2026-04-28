using ItemTradeApp.Features.Shared.Emails.Contracts;
using ItemTradeApp.Resources.EmailTemplates.Models;

namespace ItemTradeApp.Features.Shared.Emails.Services;

public interface IEmailGenerationService
{
    Task SendOfferCreatedAsync(int userId, OfferCreatedEmailModel model, CancellationToken ct);
    Task SendTradeCreatedAsync(int userId, TradeCreatedEmailModel model, CancellationToken ct);
    Task SendTradeFromCounterOfferCreatedAsync(int userId, TradeFromCounterOfferCreatedEmailModel model, CancellationToken ct);

    Task SendTradeCompletedAsync(int userId, TradeFinishedEmailModel model, CancellationToken ct);
    Task SendTradeCancelledAsync(int userId, TradeFinishedEmailModel model, CancellationToken ct);
}

public sealed class EmailGenerationService(
    IEmailTemplateRenderer renderer,
    IEmailDispatcher dispatcher) : IEmailGenerationService
{
    public async Task SendOfferCreatedAsync(int userId, OfferCreatedEmailModel model, CancellationToken ct)
        => await SendAsync(userId, "offer-created", $"New offer \"{model.Name}\"", model, ct);

    public async Task SendTradeCreatedAsync(int userId, TradeCreatedEmailModel model, CancellationToken ct)
        => await SendAsync(userId, "trade-created", $"New trade created", model, ct);

    public async Task SendTradeFromCounterOfferCreatedAsync(int userId, TradeFromCounterOfferCreatedEmailModel model, CancellationToken ct)
        => await SendAsync(userId, "trade-from-counteroffer-created", $"Trade created from counter offer", model, ct);

    public async Task SendTradeCompletedAsync(int userId, TradeFinishedEmailModel model, CancellationToken ct)
        => await SendAsync(userId, "trade-completed", $"Trade completed successfully", model, ct);

    public async Task SendTradeCancelledAsync(int userId, TradeFinishedEmailModel model, CancellationToken ct)
        => await SendAsync(userId, "trade-cancelled", $"Trade cancelled", model, ct);

    private async Task SendAsync<TModel>(
        int userId,
        string templateName,
        string subject,
        TModel model,
        CancellationToken ct)
    {
        var html = await renderer.RenderAsync(templateName, model, ct);

        await dispatcher.EnqueueAsync(new EmailJob(
            userId,
            subject,
            html), ct);
    }
}