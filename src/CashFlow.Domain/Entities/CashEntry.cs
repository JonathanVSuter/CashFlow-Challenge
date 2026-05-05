using CashFlow.Domain.Common;
using CashFlow.Domain.Enums;

namespace CashFlow.Domain.Entities;

public sealed class CashEntry : Entity
{
    private CashEntry() { }

    public CashEntry(EntryType type, decimal amount, string description, DateTime occurredAt)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));

        Id = Guid.NewGuid();
        Type = type;
        Amount = amount;
        Description = description.Trim();
        OccurredAt = occurredAt;
        CreatedAt = DateTime.UtcNow;
    }

    public EntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
