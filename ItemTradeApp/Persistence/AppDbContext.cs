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

    public virtual DbSet<Level> Levels { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<counteroffer> counteroffers { get; set; }

    public virtual DbSet<game> games { get; set; }

    public virtual DbSet<genre> genres { get; set; }

    public virtual DbSet<item> items { get; set; }

    public virtual DbSet<listingitem> listingitems { get; set; }

    public virtual DbSet<listingofferitem> listingofferitems { get; set; }

    public virtual DbSet<offer> offers { get; set; }

    public virtual DbSet<offerstatus> offerstatuses { get; set; }

    public virtual DbSet<profileinfo> profileinfos { get; set; }

    public virtual DbSet<trade> trades { get; set; }

    public virtual DbSet<tradestatus> tradestatuses { get; set; }

    public virtual DbSet<userrole> userroles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Level>(entity =>
        {
            entity.HasKey(e => e.id).HasName("level_pk");

            entity.ToTable("Level");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.name).HasMaxLength(50);

            entity.HasOne(d => d.user).WithMany(p => p.Levels)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("level_user");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.id).HasName("user_pk");

            entity.ToTable("User");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.auth0userid).HasMaxLength(128);
            entity.Property(e => e.email).HasMaxLength(100);
            entity.Property(e => e.stripecustomerid).HasMaxLength(128);
            entity.Property(e => e.tokenexpdate).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<counteroffer>(entity =>
        {
            entity.HasKey(e => e.id).HasName("listingoffer_pk");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.creationdate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.offer).WithMany(p => p.counteroffers)
                .HasForeignKey(d => d.offer_id)
                .HasConstraintName("counteroffers_offer");

            entity.HasOne(d => d.offerstatus).WithMany(p => p.counteroffers)
                .HasForeignKey(d => d.offerstatus_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listingoffer_offerstatus");

            entity.HasOne(d => d.user).WithMany(p => p.counteroffers)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listingoffer_user");
        });

        modelBuilder.Entity<game>(entity =>
        {
            entity.HasKey(e => e.id).HasName("game_pk");

            entity.ToTable("game");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.name).HasMaxLength(50);
            entity.Property(e => e.photourl).HasMaxLength(200);

            entity.HasOne(d => d.genre).WithMany(p => p.games)
                .HasForeignKey(d => d.genre_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_genre");
        });

        modelBuilder.Entity<genre>(entity =>
        {
            entity.HasKey(e => e.id).HasName("genre_pk");

            entity.ToTable("genre");

            entity.Property(e => e.id).ValueGeneratedNever();
        });

        modelBuilder.Entity<item>(entity =>
        {
            entity.HasKey(e => e.id).HasName("item_pk");

            entity.ToTable("item");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.name).HasMaxLength(50);
            entity.Property(e => e.photourl).HasMaxLength(200);

            entity.HasOne(d => d.game).WithMany(p => p.items)
                .HasForeignKey(d => d.game_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("item_game");
        });

        modelBuilder.Entity<listingitem>(entity =>
        {
            entity.HasKey(e => e.id).HasName("listingitems_pk");

            entity.Property(e => e.id).ValueGeneratedNever();

            entity.HasOne(d => d.item).WithMany(p => p.listingitems)
                .HasForeignKey(d => d.item_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listingitems_item");

            entity.HasOne(d => d.offer).WithMany(p => p.listingitems)
                .HasForeignKey(d => d.offer_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listingitems_listing");
        });

        modelBuilder.Entity<listingofferitem>(entity =>
        {
            entity.HasKey(e => e.id).HasName("listingofferitems_pk");

            entity.Property(e => e.id).ValueGeneratedNever();

            entity.HasOne(d => d.item).WithMany(p => p.listingofferitems)
                .HasForeignKey(d => d.item_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listingofferitems_item");

            entity.HasOne(d => d.listingoffer).WithMany(p => p.listingofferitems)
                .HasForeignKey(d => d.listingoffer_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listingofferitems_listingoffer");
        });

        modelBuilder.Entity<offer>(entity =>
        {
            entity.HasKey(e => e.id).HasName("listing_pk");

            entity.ToTable("offer");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.expdate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.user).WithMany(p => p.offers)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("listing_user");
        });

        modelBuilder.Entity<offerstatus>(entity =>
        {
            entity.HasKey(e => e.id).HasName("status_pk");

            entity.ToTable("offerstatus");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.status).HasMaxLength(50);
        });

        modelBuilder.Entity<profileinfo>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("profileinfo_pk");

            entity.ToTable("profileinfo");

            entity.Property(e => e.user_id).ValueGeneratedNever();
            entity.Property(e => e.description).HasMaxLength(500);
            entity.Property(e => e.nickname).HasMaxLength(20);

            entity.HasOne(d => d.user).WithOne(p => p.profileinfo)
                .HasForeignKey<profileinfo>(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("profileinfo_user");
        });

        modelBuilder.Entity<trade>(entity =>
        {
            entity.HasKey(e => e.id).HasName("trade_pk");

            entity.ToTable("trade");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.buyerfeedback).HasMaxLength(200);
            entity.Property(e => e.completitiondate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.creationdate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.customer).WithMany(p => p.tradecustomers)
                .HasForeignKey(d => d.customer_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trade_user");

            entity.HasOne(d => d.middlemanuser).WithMany(p => p.trademiddlemanusers)
                .HasForeignKey(d => d.middlemanuser_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trade_middleman");

            entity.HasOne(d => d.offer).WithMany(p => p.trades)
                .HasForeignKey(d => d.offer_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trade_listing");

            entity.HasOne(d => d.tradestatus).WithMany(p => p.trades)
                .HasForeignKey(d => d.tradestatus_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("trade_tradestatus");

            entity.HasOne(d => d.user).WithMany(p => p.tradeusers)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_buyer");
        });

        modelBuilder.Entity<tradestatus>(entity =>
        {
            entity.HasKey(e => e.id).HasName("tradestatus_pk");

            entity.ToTable("tradestatus");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.status).HasMaxLength(50);
        });

        modelBuilder.Entity<userrole>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("admin_pk");

            entity.Property(e => e.user_id).ValueGeneratedNever();
            entity.Property(e => e.rolename).HasMaxLength(20);

            entity.HasOne(d => d.user).WithOne(p => p.userrole)
                .HasForeignKey<userrole>(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("userroles_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
