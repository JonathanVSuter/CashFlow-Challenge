using CashFlow.Application.Abstractions.Data;
using CashFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<CashEntry> CashEntries => Set<CashEntry>();
    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CashEntry>(b =>
        {
            b.ToTable("cash_entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Description).HasMaxLength(250);
            b.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<DailyBalance>(b =>
        {
            b.ToTable("daily_balances");
            b.HasKey(x => x.Id);
            b.Property(x => x.TotalCredits).HasPrecision(18, 2);
            b.Property(x => x.TotalDebits).HasPrecision(18, 2);
            b.HasIndex(x => x.Date).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(200);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CreatedAt);
        });
    }
}
