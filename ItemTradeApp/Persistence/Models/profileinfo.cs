using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class profileinfo
{
    public int user_id { get; set; }

    public string nickname { get; set; } = null!;

    public string description { get; set; } = null!;

    public virtual User user { get; set; } = null!;
}
