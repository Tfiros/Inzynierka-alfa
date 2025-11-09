using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class Level
{
    public int id { get; set; }

    public string name { get; set; } = null!;

    public int requiredexp { get; set; }

    public int user_id { get; set; }

    public virtual User user { get; set; } = null!;
}
