using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
namespace CashFlow.Application.Features.DailyBalances.Queries.GetDailyBalanceByDate;
public sealed record GetDailyBalanceByDateQuery(DateOnly Date) : IQuery<DailyBalanceDto?>;
