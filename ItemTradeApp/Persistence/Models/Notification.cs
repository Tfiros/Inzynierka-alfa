using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("notification")]
public class Notification
{
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
    [Column("is_deleted")] 
    public bool IsDeleted { get; set; }

    [Column("read_at")]
    public DateTimeOffset? ReadAt { get; set; }

    public User User { get; set; } = null!;
}