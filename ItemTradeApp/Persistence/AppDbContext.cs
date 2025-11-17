using System;
using System.Collections.Generic;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Persistence;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<CounterOffer> CounterOffers { get; set; }

    public virtual DbSet<CounterOfferStatus> CounterOfferStatuses { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<ListingCounterOfferItem> ListingCounterOfferItems { get; set; }

    public virtual DbSet<ListingItem> ListingItems { get; set; }

    public virtual DbSet<Offer> Offers { get; set; }

    public virtual DbSet<OfferStatus> OfferStatuses { get; set; }

    public virtual DbSet<ProfileInfo> ProfileInfos { get; set; }

    public virtual DbSet<Trade> Trades { get; set; }

    public virtual DbSet<TradeStatus> TradeStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("user_pk");

            entity.ToTable("User");

            entity.Property(e => e.Auth0UserID).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.StripeCustomerID).HasMaxLength(128);
            entity.Property(e => e.TokenExpDate).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<CounterOffer>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("listingoffer_pk");

            entity.ToTable("counter_offer");

            entity.Property(e => e.CreationDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Offer).WithMany(p => p.CounterOffers)
                .HasForeignKey(d => d.Offer_Id)
                .HasConstraintName("counteroffers_offer");

            entity.HasOne(d => d.OfferStatus).WithMany(p => p.CounterOffers)
                .HasForeignKey(d => d.OfferStatus_Id)
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

        modelBuilder.Entity<ListingItem>(entity =>
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

            entity.Property(e => e.ExpDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.OfferStatus).WithMany(p => p.Offers)
                .HasForeignKey(d => d.OfferStatus_ID)
                .HasConstraintName("offer_offer_status");

            entity.HasOne(d => d.User).WithMany(p => p.Offers)
                .HasForeignKey(d => d.User_ID)
                .HasConstraintName("listing_user");
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

            entity.Property(e => e.BuyerFeedback).HasMaxLength(200);
            entity.Property(e => e.CompletitionDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreationDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerTrades)
                .HasForeignKey(d => d.Customer_ID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trade_user");

            entity.HasOne(d => d.MiddlemanUser).WithMany(p => p.TrademiddlemanUsers)
                .HasForeignKey(d => d.MiddlemanUser_ID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trade_middleman");

            entity.HasOne(d => d.Offer).WithMany(p => p.Trades)
                .HasForeignKey(d => d.Offer_ID)
                .HasConstraintName("trade_listing");

            entity.HasOne(d => d.TradeStatus).WithMany(p => p.Trades)
                .HasForeignKey(d => d.TradeStatus_ID)
                .HasConstraintName("trade_tradestatus");

            entity.HasOne(d => d.User).WithMany(p => p.OwningTrades)
                .HasForeignKey(d => d.User_ID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_buyer");
        });

        modelBuilder.Entity<TradeStatus>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("tradestatus_pk");

            entity.ToTable("trade_status");

            entity.Property(e => e.StatusName).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
