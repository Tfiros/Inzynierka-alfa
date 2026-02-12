using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record CounterOfferDto
{
    public int ID { get; set; }

    public int ParentOffer_ID { get; set; }
    public Offer ParentOffer { get; set; } = null!;

    public int User_ID { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreationDate { get; set; }

    public int CounterOfferStatus_ID { get; set; }

    public ICollection<CounterOfferItems> Items { get; set; } = new List<CounterOfferItems>();
}
