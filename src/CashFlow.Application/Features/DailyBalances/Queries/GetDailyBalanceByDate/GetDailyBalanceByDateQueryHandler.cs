using CashFlow.Application.Abstractions.Data;
using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CashFlow.Application.Features.DailyBalances.Queries.GetDailyBalanceByDate;

public sealed class GetDailyBalanceByDateQueryHandler : IQueryHandler<GetDailyBalanceByDateQuery, DailyBalanceDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public GetDailyBalanceByDateQueryHandler(IApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<DailyBalanceDto?> HandleAsync(GetDailyBalanceByDateQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = $"daily-balance:{query.Date:yyyy-MM-dd}";
        if (_cache.TryGetValue(cacheKey, out DailyBalanceDto? cached)) return cached;

        var balance = await _dbContext.DailyBalances.AsNoTracking().FirstOrDefaultAsync(x => x.Date == query.Date, cancellationToken);
        if (balance is null) return null;

        var dto = new DailyBalanceDto(balance.Date, balance.TotalCredits, balance.TotalDebits, balance.Balance, balance.LastUpdatedAt);
        _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(30));
        return dto;
    }
}
