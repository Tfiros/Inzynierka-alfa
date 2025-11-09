using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class trade
{
    public int id { get; set; }

    public int offer_id { get; set; }

    public int tokencost { get; set; }

    public DateTime creationdate { get; set; }

    public DateTime? completitiondate { get; set; }

    public string? buyerfeedback { get; set; }

    public int? buyerpositivity { get; set; }

    public int tradestatus_id { get; set; }

    public int customer_id { get; set; }

    public int middlemanuser_id { get; set; }

    public int user_id { get; set; }

    public virtual User customer { get; set; } = null!;

    public virtual User middlemanuser { get; set; } = null!;

    public virtual offer offer { get; set; } = null!;

    public virtual tradestatus tradestatus { get; set; } = null!;

    public virtual User user { get; set; } = null!;
}
