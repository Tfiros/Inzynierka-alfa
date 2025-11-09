using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class userrole
{
    public int user_id { get; set; }

    public string rolename { get; set; } = null!;

    public virtual User user { get; set; } = null!;
}
