namespace ItemTradeApp.Persistence;

public enum OfferStatuses
{
    Active = 1,
    Expired = 2,
    InRealization = 3,
    Closed = 4
}

public enum CounterOfferStatuses
{
    Pending = 1,
    Accepted = 2,
    Denied = 3
}

public enum TradeStatuses
{
    New = 1,
    AwaitingSellerDeposit = 2,
    AwaitingBuyerDeposit = 3,
    InRealization = 4,
    SuccesfulRealization = 5,
    Failed = 6
}