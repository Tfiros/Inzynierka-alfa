using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("User")]
public partial class User
{
    [Column("id")]
    public int ID { get; set; }
    [Column("auth0_userid")]
    public string Auth0UserID { get; set; } = null!;
    [Column("stripe_customer_id")]
    public string StripeCustomerID { get; set; } = null!;
    [Column("email")]
    public string Email { get; set; } = null!;
    [Column("date_of_birth")]
    public DateOnly DateOfBirth { get; set; }
    [Column("tokens")]
    public int Tokens { get; set; }
    [Column("exp")]
    public int Exp { get; set; }
    [Column("token_exp_date")]
    public DateTime TokenExpDate { get; set; }
    [Column("registration_date")]
    public DateOnly RegistrationDate { get; set; }

    public virtual ICollection<CounterOffer> CounterOffers { get; set; } = new List<CounterOffer>();

    public virtual ICollection<Offer> Offers { get; set; } = new List<Offer>();

    public virtual profile_info? ProfileInfo { get; set; }

    public virtual ICollection<Trade> CustomerTrades { get; set; } = new List<Trade>();

    public virtual ICollection<Trade> TrademiddlemanUsers { get; set; } = new List<Trade>();

    public virtual ICollection<Trade> OwningTrades { get; set; } = new List<Trade>();
}
