using CashFlow.Domain.Enums;
namespace CashFlow.Api.Contracts;
public sealed record CreateCashEntryRequest(EntryType Type, decimal Amount, string Description, DateTime OccurredAt);
