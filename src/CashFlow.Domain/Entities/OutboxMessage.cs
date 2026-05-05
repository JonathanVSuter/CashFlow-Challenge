using CashFlow.Domain.Common;

namespace CashFlow.Domain.Entities;

public sealed class OutboxMessage : Entity
{
    private OutboxMessage() { }

    public OutboxMessage(string type, string payload)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        CreatedAt = DateTime.UtcNow;
        Status = OutboxMessageStatus.Pending;
    }

    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }
    public OutboxMessageStatus Status { get; private set; }

    public void MarkAsPublished()
    {
        Status = OutboxMessageStatus.Published;
        PublishedAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        Attempts++;
        Error = error;
        if (Attempts >= 5) Status = OutboxMessageStatus.Failed;
    }
}

public enum OutboxMessageStatus { Pending = 1, Published = 2, Failed = 3 }
