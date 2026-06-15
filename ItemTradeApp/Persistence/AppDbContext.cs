using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Persistence;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; } = null!;

    public virtual DbSet<CounterOffer> CounterOffers { get; set; } = null!;

    public virtual DbSet<CounterOfferStatus> CounterOfferStatuses { get; set; } = null!;
    
    public virtual DbSet<UserFavouriteOffer> UserFavouriteOffers { get; set; } = null!;

    public virtual DbSet<Game> Games { get; set; } = null!;

    public virtual DbSet<Genre> Genres { get; set; } = null!;

    public virtual DbSet<Item> Items { get; set; } = null!;
    public virtual DbSet<ItemRarity> ItemRarities { get; set; } = null!;

    public virtual DbSet<ListingCounterOfferItem> ListingCounterOfferItems { get; set; } = null!;

    public virtual DbSet<ListingItems> ListingItems { get; set; } = null!;

    public virtual DbSet<Offer> Offers { get; set; } = null!;

    public virtual DbSet<OfferStatus> OfferStatuses { get; set; } = null!;

    public virtual DbSet<ProfileInfo> ProfileInfos { get; set; } = null!;
    public virtual DbSet<TradeUrl> TradeUrls { get; set; } = null!;
    public virtual DbSet<Trade> Trades { get; set; } = null!;

    public virtual DbSet<TradeStatus> TradeStatuses { get; set; } = null!;
    public virtual DbSet<Notification> Notifications { get; set; } = null!;
    public virtual DbSet<Email> Emails { get; set; } = null!;
    public virtual DbSet<Rate> Rates { get; set; } = null!;
    public virtual DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public virtual DbSet<ConversationMember> ConversationMembers { get; set; } = null!;

    public virtual DbSet<ChatConversation> ChatConversations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var fk in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("user_pk");
            entity
                .Property(x => x.TokenExpDate)
                .HasColumnType("timestamptz");

            entity.ToTable("User");

            entity.Property(e => e.Auth0UserID).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.StripeCustomerID).HasMaxLength(128);
        });

        modelBuilder.Entity<UserFavouriteOffer>(entity =>
        {
            entity.HasKey(e => new {  e.Offer_ID, e.User_ID });

            entity.ToTable("user_favourite_offer");

            entity.Property(e => e.AddedAt).HasColumnType("timestamptz");

            entity.HasOne(u => u.User).WithMany(f => f.FavouriteOffers)
                .HasForeignKey(u => u.User_ID);
            entity.HasOne(o => o.Offer).WithMany(f => f.FavoruitedBy)
                .HasForeignKey(o => o.Offer_ID);
        });

        modelBuilder.Entity<CounterOffer>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("listingoffer_pk");

            entity.ToTable("counter_offer");

            entity.Property(e => e.CreationDate).HasColumnType("timestamptz");

            entity.HasOne(d => d.Offer).WithMany(p => p.CounterOffers)
                .HasForeignKey(d => d.Offer_Id)
                .HasConstraintName("counteroffers_offer");

            entity.HasOne(d => d.OfferStatus).WithMany(p => p.CounterOffers)
                .HasForeignKey(d => d.CounterOfferStatus_Id)
                .HasConstraintName("counter_offer_status_co");

            entity.HasOne(d => d.User).WithMany(p => p.CounterOffers)
                .HasForeignKey(d => d.User_ID)
                .HasConstraintName("listingoffer_user");
        });

        modelBuilder.Entity<CounterOfferStatus>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("co_status_pk");

            entity.ToTable("counter_offer_status");

            entity.Property(e => e.StatusName).HasMaxLength(50);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("game_pk");

            entity.ToTable("game");

            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Photo_URL).HasMaxLength(200);

            entity.HasOne(d => d.Genre).WithMany(p => p.Games)
                .HasForeignKey(d => d.Genre_ID)
                .HasConstraintName("game_genre");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("genre_pk");

            entity.ToTable("genre");

            entity.Property(e => e.Name).HasMaxLength(20);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("item_pk");

            entity.ToTable("item");

            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Photo_URL).HasMaxLength(200);

            entity.HasOne(d => d.Game).WithMany(p => p.Items)
                .HasForeignKey(d => d.Game_ID)
                .HasConstraintName("item_game");
            entity.Property(x => x.ItemRarityId).HasColumnName("item_rarity_id");

            entity.HasOne(x => x.ItemRarity)
                .WithMany(r => r.Items)
                .HasForeignKey(x => x.ItemRarityId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ItemRarity>(e =>
        {
            e.ToTable("item_rarity");

            e.HasKey(x => x.ID);

            e.Property(x => x.RarityName)
                .HasColumnName("rarity_name")
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.GameId).HasColumnName("game_id");

            e.HasOne(x => x.Game)
                .WithMany(g => g.ItemRarities)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ListingCounterOfferItem>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("listingofferitems_pk");

            entity.HasOne(d => d.CounterOffer).WithMany(p => p.ListingCounterOfferItems)
                .HasForeignKey(d => d.CounterOffers_ID)
                .HasConstraintName("listingofferitems_listingoffer");

            entity.HasOne(d => d.Item).WithMany(p => p.ListingCounterOfferItems)
                .HasForeignKey(d => d.Item_ID)
                .HasConstraintName("listingofferitems_item");
        });

        modelBuilder.Entity<ListingItems>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("listingitems_pk");

            entity.HasOne(d => d.Item).WithMany(p => p.ListingItems)
                .HasForeignKey(d => d.Item_ID)
                .HasConstraintName("listingitems_item");

            entity.HasOne(d => d.Offer).WithMany(p => p.ListingItems)
                .HasForeignKey(d => d.Offer_ID)
                .HasConstraintName("listingitems_listing");
        });

        modelBuilder.Entity<Offer>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("listing_pk");

            entity.ToTable("offer");
            
            entity.Property(e => e.CreationDate).HasColumnType("timestamptz");

            entity.HasOne(d => d.OfferStatus).WithMany(p => p.Offers)
                .HasForeignKey(d => d.OfferStatus_ID)
                .HasConstraintName("offer_offer_status");

            entity.HasOne(d => d.User).WithMany(p => p.Offers)
                .HasForeignKey(d => d.User_ID)
                .HasConstraintName("listing_user");
            
            entity.Property(e => e.Title).HasMaxLength(120);
            
            entity.Property(e => e.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<OfferStatus>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("offer_status_pk");

            entity.ToTable("offer_status");
        });

        modelBuilder.Entity<ProfileInfo>(entity =>
        {
            entity.HasKey(e => e.User_ID).HasName("profile_info_pk");

            entity.ToTable("profile_info");

            entity.Property(e => e.User_ID).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Nickname).HasMaxLength(20);

            entity.HasOne(d => d.User).WithOne(p => p.ProfileInfo)
                .HasForeignKey<ProfileInfo>(d => d.User_ID)
                .HasConstraintName("profileinfo_user");
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("trade_pk");

            entity.ToTable("trade");

            entity.Property(e => e.CompletitionDate).HasColumnType("timestamptz");
            entity.Property(e => e.CreationDate).HasColumnType("timestamptz");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerTrades)
                .HasForeignKey(d => d.Customer_ID)
                .HasConstraintName("trade_user");

            entity.HasOne(d => d.MiddlemanUser).WithMany(p => p.TrademiddlemanUsers)
                .HasForeignKey(d => d.MiddlemanUser_ID)
                .HasConstraintName("trade_middleman");

            entity.HasOne(d => d.Offer).WithMany(p => p.Trades)
                .HasForeignKey(d => d.Offer_ID)
                .HasConstraintName("trade_listing");

            entity.HasOne(d => d.TradeStatus).WithMany(p => p.Trades)
                .HasForeignKey(d => d.TradeStatus_ID)
                .HasConstraintName("trade_tradestatus");
            entity.HasOne(t => t.AcceptedCounterOffer)
                .WithMany()
                .HasForeignKey(t => t.AcceptedCounterOffer_ID)
                .OnDelete(DeleteBehavior.Restrict);
           
            entity.HasMany(t => t.Urls)
                .WithOne(u => u.Trade)
                .HasForeignKey(u => u.TradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TradeUrl>(b =>
        {
            b.Property(x => x.PhotoUrl).HasMaxLength(2048);
        });
        modelBuilder.Entity<TradeStatus>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("tradestatus_pk");

            entity.ToTable("trade_status");

            entity.Property(e => e.StatusName).HasMaxLength(50);
        });
        modelBuilder.Entity<Rate>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.TradeId })
                .HasName("PK_Rate");

            entity.ToTable("rate");

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.HasOne(d => d.User)
                .WithMany(p => p.Rates)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Rate_User");

            entity.HasOne(d => d.Trade)
                .WithMany(p => p.Rates)
                .HasForeignKey(d => d.TradeId)
                .HasConstraintName("FK_Rate_Trade");
            
        });
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notification");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Message).HasColumnName("message").HasMaxLength(400).IsRequired();

            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.ReadAt).HasColumnName("read_at");
            entity.HasOne(x => x.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_notification_user");
        });
        modelBuilder.Entity<Email>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

            entity.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasColumnName("body").HasMaxLength(10000).IsRequired();

            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.SentAt).HasColumnName("sent_at");

            entity.HasOne(x => x.User)
                .WithMany(u => u.Emails)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_email_outbox_user");
        });
        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.ToTable("chat_conversation");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()");

            entity.HasMany(x => x.Members)
                .WithOne(x => x.ChatConversation)
                .HasForeignKey(x => x.ChatConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Messages)
                .WithOne(x => x.ChatConversation)
                .HasForeignKey(x => x.ChatConversationId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.Property(x => x.ClosedAt).HasColumnType("timestamptz");
            entity.HasOne(x => x.Trade)
                .WithMany().HasForeignKey(x => x.TradeId)
                .HasConstraintName("chat_conversation-trade");
        });
        modelBuilder.Entity<ConversationMember>(entity =>
        {
            entity.ToTable("conversation_member");

            entity.HasKey(x => new { x.UserId, x.ChatConversationId });

            entity.HasOne(x => x.User)
                .WithMany(u => u.Chats)
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(u => u.ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ChatConversation)
                .WithMany(c => c.Members)
                .HasForeignKey(x => x.ChatConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LastReadMessage)
                .WithMany()
                .HasForeignKey(x => new { x.LastReadMessageId, x.LastReadMessageChatConversationId })
                .HasPrincipalKey(m => new { m.Id, m.ChatConversationId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(x => new { x.Id, x.ChatConversationId });

            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Message)
                .HasColumnType("text")
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()");

            entity.Property(x => x.EditedAt).HasColumnType("timestamptz");
            entity.Property(x => x.DeletedAt).HasColumnType("timestamptz");

            entity.HasOne(x => x.ChatConversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(x => x.ChatConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
