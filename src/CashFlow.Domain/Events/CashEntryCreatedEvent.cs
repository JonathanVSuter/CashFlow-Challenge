using CashFlow.Domain.Enums;

namespace CashFlow.Domain.Events;

public sealed record CashEntryCreatedEvent(
    Guid EntryId,
    EntryType Type,
    decimal Amount,
    DateTime OccurredAt
);
