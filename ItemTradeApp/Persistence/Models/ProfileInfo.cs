using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("profile_info")]
public partial class profile_info
{
    [Column("id")]
    public int User_ID { get; set; }
    [Column("nick_name")]
    public string NickName { get; set; } = null!;
    [Column("description")]
    public string Description { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
