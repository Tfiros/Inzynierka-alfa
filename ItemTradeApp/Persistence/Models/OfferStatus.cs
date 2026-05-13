using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("offer_status")]
public partial class OfferStatus
{
    [Column("id")]
    public int ID { get; set; }
    [Column("status_name")]
    public string StatusName { get; set; } = null!;

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
