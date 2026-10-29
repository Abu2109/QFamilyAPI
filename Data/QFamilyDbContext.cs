
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QFamilyAPI.Models;

namespace QFamilyAPI.Data;

public partial class QFamilyDbContext : DbContext
{
    public QFamilyDbContext(
        DbContextOptions<QFamilyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Child> Children { get; set; }

    public virtual DbSet<Device> Devices { get; set; }

    public virtual DbSet<UsageRule> UsageRules { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Child>(entity =>
        {
            entity.HasKey(e => e.ChildId)
                .HasName("PK__Children__BEFA07161E75A2BA");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.Property(e => e.FullName)
                .HasMaxLength(100);

            entity.HasOne(d => d.Parent)
                .WithMany(p => p.ChildParents)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Children_Users");

            entity.HasOne(d => d.User)
                .WithMany(p => p.ChildUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Children_UserAccount");
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.DeviceId)
                .HasName("PK__Devices__49E123116916FC45");

            entity.HasIndex(e => e.DeviceCode,
                "UQ__Devices__AFFB3E95D7B125BB")
                .IsUnique();

            entity.Property(e => e.DeviceCode)
                .HasMaxLength(100);

            entity.Property(e => e.DeviceName)
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.Platform)
                .HasMaxLength(30)
                .HasDefaultValue("Android");

            entity.HasOne(d => d.Child)
                .WithMany(p => p.Devices)
                .HasForeignKey(d => d.ChildId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Devices_Children");
        });

        modelBuilder.Entity<UsageRule>(entity =>
        {
            entity.HasKey(e => e.RuleId)
                .HasName("PK__UsageRul__110458E2BF86FBC5");

            entity.Property(e => e.DailyLimitMinutes)
                .HasDefaultValue(120);

            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true);

            entity.HasOne(d => d.Child)
                .WithMany(p => p.UsageRules)
                .HasForeignKey(d => d.ChildId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageRules_Children");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId)
                .HasName("PK__Users__1788CC4C06ADEB7A");

            entity.HasIndex(e => e.Email,
                "UQ__Users__A9D10534BF719964")
                .IsUnique();

            entity.HasIndex(e => e.Username,
                "UX_Users_Username")
                .IsUnique()
                .HasFilter(
                    "([Username] IS NOT NULL AND [Username]<>N'')");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.Property(e => e.Email)
                .HasMaxLength(150);

            entity.Property(e => e.FullName)
                .HasMaxLength(100);

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(500);

            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("Parent");

            entity.Property(e => e.Username)
                .HasMaxLength(50);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder);
}
