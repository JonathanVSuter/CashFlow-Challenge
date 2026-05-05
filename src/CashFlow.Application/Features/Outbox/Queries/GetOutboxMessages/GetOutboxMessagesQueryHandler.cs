using CashFlow.Application.Abstractions.Data;
using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Application.Features.Outbox.Queries.GetOutboxMessages;

public sealed class GetOutboxMessagesQueryHandler : IQueryHandler<GetOutboxMessagesQuery, IReadOnlyCollection<OutboxMessageDto>>
{
    private readonly IApplicationDbContext _dbContext;
    public GetOutboxMessagesQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<OutboxMessageDto>> HandleAsync(GetOutboxMessagesQuery query, CancellationToken cancellationToken)
    {
        return await _dbContext.OutboxMessages.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OutboxMessageDto(x.Id, x.Type, x.Status, x.Attempts, x.CreatedAt, x.PublishedAt, x.Error))
            .ToListAsync(cancellationToken);
    }
}
