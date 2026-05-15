using CashFlow.Application.Abstractions.Data;
using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Application.Features.CashEntries.Queries.GetCashEntries;

public sealed class GetCashEntriesQueryHandler : IQueryHandler<GetCashEntriesQuery, IReadOnlyCollection<CashEntryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    public GetCashEntriesQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<CashEntryDto>> HandleAsync(GetCashEntriesQuery query, CancellationToken cancellationToken)
    {
        var entries = _dbContext.CashEntries.AsNoTracking();
        if (query.Date.HasValue)
        {
            var start = new DateTimeOffset(
                query.Date.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);

            var end = new DateTimeOffset(
                query.Date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            entries = entries.Where(x => x.OccurredAt >= start && x.OccurredAt < end);
        }

        return await entries
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new CashEntryDto(x.Id, x.Type, x.Amount, x.Description, x.OccurredAt, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
