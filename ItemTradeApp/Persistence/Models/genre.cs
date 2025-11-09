using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class genre
{
    public int id { get; set; }

    public int name { get; set; }

    public virtual ICollection<game> games { get; set; } = new List<game>();
}
