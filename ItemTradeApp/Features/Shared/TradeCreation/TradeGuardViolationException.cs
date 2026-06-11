namespace ItemTradeApp.Features.Shared.TradeCreation;

public sealed class TradeGuardViolationException(string exceptionMessage) : Exception(exceptionMessage);