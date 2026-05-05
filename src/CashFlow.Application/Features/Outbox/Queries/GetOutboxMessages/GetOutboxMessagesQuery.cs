using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
namespace CashFlow.Application.Features.Outbox.Queries.GetOutboxMessages;
public sealed record GetOutboxMessagesQuery() : IQuery<IReadOnlyCollection<OutboxMessageDto>>;
