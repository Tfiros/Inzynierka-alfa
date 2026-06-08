using ItemTradeApp.Features.Shared.Emails.Contracts;
using ItemTradeApp.Features.Shared.Emails.Mappers;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared.Emails.Services;

public interface IEmailGenerationService
{
    Task SendOfferCreatedAsync(
        int userId,
        Offer offer,
        CancellationToken ct);

    Task SendTradeCreatedAsync(
        int userId,
        string buyerNick,
        string sellerNick,
        Trade trade,
        Offer offer,
        CancellationToken ct);

    Task SendTradeFromCounterOfferCreatedAsync(
        int userId,
        string buyerNick,
        string sellerNick,
        Trade trade,
        Offer offer,
        CancellationToken ct);

    Task SendTradeCompletedAsync(
        int userId,
        string buyerNick,
        string sellerNick,
        string middlemanNick,
        Trade trade,
        Offer offer,
        CancellationToken ct);

    Task SendTradeCancelledAsync(
        int userId,
        string buyerNick,
        string sellerNick,
        string middlemanNick,
        Trade trade,
        Offer offer,
        CancellationToken ct);
}

public sealed class EmailGenerationService(
    IEmailTemplateRenderer renderer,
    IEmailDispatcher dispatcher) : IEmailGenerationService
{
    public async Task SendOfferCreatedAsync(int userId, Offer offer , CancellationToken ct)
    {
        var emailModel = EmailTemplateMapper.MapToOfferCreatedEmailModel(offer);
        await SendAsync(userId, "offer-created", $"Oferta \"{emailModel.Name}\" utworzona", emailModel, ct);
    }

    public async Task SendTradeCreatedAsync(int userId,
        string buyerNick, 
        string sellerNick, 
        Trade trade,
        Offer offer, CancellationToken ct)
    {
        var emailModel = EmailTemplateMapper.MapToTradeCreatedEmailModel(buyerNick, sellerNick, trade,offer);
        await SendAsync(userId, "trade-created", $"Nowy trade utworzony", emailModel, ct);
    }

    public async Task SendTradeFromCounterOfferCreatedAsync(int userId, string buyerNick,
        string sellerNick,
        Trade trade,
        Offer offer, CancellationToken ct)
    {
        var emailModel = EmailTemplateMapper.MapToTradeFromCounterOfferCreatedEmailModel(buyerNick, sellerNick, trade,offer);
        await SendAsync(userId, "trade-from-counteroffer-created", $"Trade created from counter offer", emailModel, ct);
    }

    public async Task SendTradeCompletedAsync(int userId, string buyerNick,
        string sellerNick,
        string middlemanNick,
        Trade trade,
        Offer offer, CancellationToken ct)
    {
        var emailModel = EmailTemplateMapper.MapToTradeFinishedEmailModel(buyerNick, sellerNick, trade, offer, middlemanNick);
        await SendAsync(userId, "trade-completed", $"Trade completed successfully", emailModel, ct);
    }

    public async Task SendTradeCancelledAsync(int userId, string buyerNick,
        string sellerNick,
        string middlemanNick,
        Trade trade,
        Offer offer, CancellationToken ct)
    {
        var emailModel = EmailTemplateMapper.MapToTradeFinishedEmailModel(buyerNick, sellerNick, trade, offer, middlemanNick); 
        await SendAsync(userId, "trade-cancelled", $"Trade cancelled", emailModel, ct);
    }

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