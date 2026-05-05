using CashFlow.Domain.Entities;
namespace CashFlow.Application.Common.Models;
public sealed record OutboxMessageDto(Guid Id, string Type, OutboxMessageStatus Status, int Attempts, DateTime CreatedAt, DateTime? PublishedAt, string? Error);
