using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("user_favourite_offer")]
public class UserFavouriteOffer
{
    [Column("user_id")]
    public int User_ID { get; set; }
    [Column("offer_id")]
    public int Offer_ID { get; set; }
    [Column("added_at")]
    public DateTime AddedAt { get; set; }

    public virtual User User { get; set; } = null!;
    
    public virtual Offer Offer { get; set; } = null!;

}