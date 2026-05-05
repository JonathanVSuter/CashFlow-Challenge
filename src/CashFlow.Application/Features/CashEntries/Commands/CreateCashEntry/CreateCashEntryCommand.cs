using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Domain.Enums;

namespace CashFlow.Application.Features.CashEntries.Commands.CreateCashEntry;

public sealed record CreateCashEntryCommand(EntryType Type, decimal Amount, string Description, DateTime OccurredAt) : ICommand<CashEntryDto>;
