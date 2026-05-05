using CashFlow.Domain.Common;
using CashFlow.Domain.Enums;

namespace CashFlow.Domain.Entities;

public sealed class DailyBalance : Entity
{
    private DailyBalance() { }

    public DailyBalance(DateOnly date)
    {
        Id = Guid.NewGuid();
        Date = date;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public DateOnly Date { get; private set; }
    public decimal TotalCredits { get; private set; }
    public decimal TotalDebits { get; private set; }
    public decimal Balance => TotalCredits - TotalDebits;
    public DateTime LastUpdatedAt { get; private set; }

    public void Apply(EntryType type, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (type == EntryType.Credit) TotalCredits += amount;
        else TotalDebits += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
