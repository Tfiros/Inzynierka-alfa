using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class tradestatus
{
    public int id { get; set; }

    public string status { get; set; } = null!;

    public virtual ICollection<trade> trades { get; set; } = new List<trade>();
}
