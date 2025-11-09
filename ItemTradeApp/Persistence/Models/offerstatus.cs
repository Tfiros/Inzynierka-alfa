using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class offerstatus
{
    public int id { get; set; }

    public string status { get; set; } = null!;

    public virtual ICollection<counteroffer> counteroffers { get; set; } = new List<counteroffer>();
}
