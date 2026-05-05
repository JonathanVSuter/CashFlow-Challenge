using CashFlow.Domain.Enums;
namespace CashFlow.Application.Common.Models;
public sealed record CashEntryDto(Guid Id, EntryType Type, decimal Amount, string Description, DateTime OccurredAt, DateTime CreatedAt);
