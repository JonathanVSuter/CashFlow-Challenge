namespace CashFlow.Application.Common.Models;
public sealed record DailyBalanceDto(DateOnly Date, decimal TotalCredits, decimal TotalDebits, decimal Balance, DateTime LastUpdatedAt);
