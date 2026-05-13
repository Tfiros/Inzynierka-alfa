using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("profile_info")]
public partial class ProfileInfo
{
    [Column("user_id")]
    public int User_ID { get; set; }
    [Column("nickname")]
    public string Nickname { get; set; } = null!;
    [Column("description")]
    public string Description { get; set; } = null!;
    [Column("image_url")]
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    public virtual User User { get; set; } = null!;
}
