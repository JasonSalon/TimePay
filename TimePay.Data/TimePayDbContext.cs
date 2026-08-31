using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimePay.Core.Models;

namespace TimePay.Data;

/// <summary>
/// Entity Framework Core database context for TimePay.
/// Uses SQLite for local persistence.
/// </summary>
public class TimePayDbContext : DbContext
{
    public DbSet<Settings> Settings { get; set; } = null!;
    public DbSet<AdminUser> AdminUsers { get; set; } = null!;
    public DbSet<Session> Sessions { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    public TimePayDbContext(DbContextOptions<TimePayDbContext> options) : base(options) { }

    /// <summary>
    /// Configure value converters for SQLite compatibility.
    /// SQLite doesn't natively support DateTimeOffset, so we store them as ISO 8601 strings
    /// which sort correctly lexicographically.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToStringConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<DateTimeOffsetToStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Settings
        modelBuilder.Entity<Settings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.MinutesPerPeso).HasColumnType("REAL").IsRequired();
        });

        // AdminUsers
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PasswordSalt).IsRequired();
        });

        // Sessions
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.Status);
        });

        // Transactions
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.TransactionId).IsUnique();
            entity.Property(e => e.Amount).HasColumnType("REAL");
            entity.Property(e => e.MinutesPerPeso).HasColumnType("REAL");
            entity.Property(e => e.MinutesAdded).HasColumnType("REAL");
            entity.HasOne(e => e.Session).WithMany().HasForeignKey(e => e.SessionId);
            entity.HasOne(e => e.AdminUser).WithMany().HasForeignKey(e => e.AdminUserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // AuditLogs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(500);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
