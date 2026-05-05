using System.Text.Json;
using CashFlow.Application.Abstractions.Data;
using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Domain.Entities;
using CashFlow.Domain.Events;

namespace CashFlow.Application.Features.CashEntries.Commands.CreateCashEntry;

public sealed class CreateCashEntryCommandHandler : ICommandHandler<CreateCashEntryCommand, CashEntryDto>
{
    private readonly IApplicationDbContext _dbContext;
    public CreateCashEntryCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<CashEntryDto> HandleAsync(CreateCashEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = new CashEntry(command.Type, command.Amount, command.Description, command.OccurredAt);
        var evt = new CashEntryCreatedEvent(entry.Id, entry.Type, entry.Amount, entry.OccurredAt);
        var outbox = new OutboxMessage(nameof(CashEntryCreatedEvent), JsonSerializer.Serialize(evt));

        _dbContext.CashEntries.Add(entry);
        _dbContext.OutboxMessages.Add(outbox);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CashEntryDto(entry.Id, entry.Type, entry.Amount, entry.Description, entry.OccurredAt, entry.CreatedAt);
    }
}
