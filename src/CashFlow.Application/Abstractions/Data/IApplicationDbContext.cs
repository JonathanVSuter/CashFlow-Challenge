using CashFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<CashEntry> CashEntries { get; }
    DbSet<DailyBalance> DailyBalances { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
