using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("offer_status")]
public partial class offer_status
{
    [Column("id")]
    public int ID { get; set; }
    [Column("status_name")]
    public int StatusName { get; set; }

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
