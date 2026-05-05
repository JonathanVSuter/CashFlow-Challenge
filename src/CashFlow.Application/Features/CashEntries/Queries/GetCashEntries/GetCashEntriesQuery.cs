using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
namespace CashFlow.Application.Features.CashEntries.Queries.GetCashEntries;
public sealed record GetCashEntriesQuery(DateOnly? Date) : IQuery<IReadOnlyCollection<CashEntryDto>>;
