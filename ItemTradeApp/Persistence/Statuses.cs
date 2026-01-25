namespace ItemTradeApp.Persistence;

public enum OfferStatuses
{
    Active = 1,
    Expired = 2,
    InRealization = 3,
    Completed = 4, 
    Canceled = 5
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
    InRealization = 2,
    SuccesfulRealization = 3,
    Failed = 4
}