namespace ItemTradeApp.Resources.NotificationsTemplates;

public static class NotificationsMessages
{
    public static NotificationTemplateDTO ReceivedCounterOfferMessage(string fromUser, string offerTitle) =>
        new NotificationTemplateDTO("Otrzymano kontrofertę!", $"Nowa kontroferta od użytkownika {fromUser} do Twojej oferty {offerTitle}");

    public static NotificationTemplateDTO CounterOfferDenied(string offerTitle) =>
        new NotificationTemplateDTO("Odrzucenie kontroferty",
            $"Twoja kontroferta do oferty {offerTitle} została odrzucona przez wystawiającego.");

    public static NotificationTemplateDTO OfferSuccessfullyAdded(string offerTitle) =>
        new NotificationTemplateDTO("Utworzono ofertę!", $"Twoja oferta {offerTitle} została poprawnie utworzona!");

    public static NotificationTemplateDTO TradeCreatedFromOffer(string offerTitle) =>
        new NotificationTemplateDTO("Utworzono Trade!", $"Utworzono trade'a z oferty {offerTitle}!");

    public static NotificationTemplateDTO CounterOfferAcceptedWithTradeCreation(string offerTitle) =>
        new NotificationTemplateDTO("Zaakceptowano kontrofertę!", $"Zaakceptowano Twoją kontrofertę do oferty {offerTitle}! Trade został utworzony!");

    public static NotificationTemplateDTO TradeStatusChanged(string offerTitle, string statusName) =>
        new NotificationTemplateDTO("Zmiana statusu Trade'a!", $"Trade od oferty {offerTitle} zmienił swój status na {statusName}.");

    public static NotificationTemplateDTO TradeCancelled(string offerTitle) =>
        new NotificationTemplateDTO("Trade anulowany!",
            "Middleman anulował Twój trade! By poznać więcej szczegółów sprawdź skrzynkę mailową!");
}