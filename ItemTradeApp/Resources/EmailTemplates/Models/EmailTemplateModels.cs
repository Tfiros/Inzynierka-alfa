namespace ItemTradeApp.Resources.EmailTemplates.Models;

public sealed class EmailItemModel
{
    public string Name { get; set; } = string.Empty;
    public int Amount { get; set; }
}

public sealed class OfferCreatedEmailModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateOnly ExpiresAt { get; set; }
    public bool IsFeatured { get; set; }

    public int TokenAmount { get; set; }
    public int TokensSpent { get; set; }
    
    public List<EmailItemModel> Items { get; set; } = new();
}

public sealed class TradeCreatedEmailModel
{
    public string BuyerNickname { get; set; } = string.Empty;
    public string SellerNickname { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public int TokensCost { get; set; }

    public string OfferName { get; set; } = string.Empty;
}

public sealed class TradeFromCounterOfferCreatedEmailModel
{
    public string BuyerNickname { get; set; } = string.Empty;
    public string SellerNickname { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public int TokensCost { get; set; }

    public string OfferName { get; set; } = string.Empty;

    public List<EmailItemModel>? BuyerItems { get; set; } = new();
    public List<EmailItemModel>? SellerItems { get; set; } = new();
}

public sealed class TradeFinishedEmailModel
{
    public string BuyerNickname { get; set; } = string.Empty;
    public string SellerNickname { get; set; } = string.Empty;
    public string MiddlemanNickname { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public int TokensCost { get; set; }

    public string OfferName { get; set; } = string.Empty;
}