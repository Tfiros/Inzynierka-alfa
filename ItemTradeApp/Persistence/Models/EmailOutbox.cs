using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("email_outbox")]
public class EmailOutbox
{
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("body")]
    public string Body { get; set; } = null!;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("sent_at")]
    public DateTimeOffset? SentAt { get; set; }

    public User User { get; set; } = null!;
}