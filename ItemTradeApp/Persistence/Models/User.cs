using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class User
{
    public int id { get; set; }

    public string auth0userid { get; set; } = null!;

    public string stripecustomerid { get; set; } = null!;

    public string email { get; set; } = null!;

    public DateOnly dateofbirth { get; set; }

    public int tokens { get; set; }

    public int exp { get; set; }

    public DateTime tokenexpdate { get; set; }

    public DateOnly registrationdate { get; set; }

    public virtual ICollection<Level> Levels { get; set; } = new List<Level>();

    public virtual ICollection<counteroffer> counteroffers { get; set; } = new List<counteroffer>();

    public virtual ICollection<offer> offers { get; set; } = new List<offer>();

    public virtual profileinfo? profileinfo { get; set; }

    public virtual ICollection<trade> tradecustomers { get; set; } = new List<trade>();

    public virtual ICollection<trade> trademiddlemanusers { get; set; } = new List<trade>();

    public virtual ICollection<trade> tradeusers { get; set; } = new List<trade>();

    public virtual userrole? userrole { get; set; }
}
