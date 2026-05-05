using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Application.Features.CashEntries.Commands.CreateCashEntry;
using CashFlow.Application.Features.CashEntries.Queries.GetCashEntries;
using CashFlow.Application.Features.DailyBalances.Queries.GetDailyBalanceByDate;
using CashFlow.Application.Features.Outbox.Queries.GetOutboxMessages;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCashFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateCashEntryCommand, CashEntryDto>, CreateCashEntryCommandHandler>();
        services.AddScoped<IQueryHandler<GetCashEntriesQuery, IReadOnlyCollection<CashEntryDto>>, GetCashEntriesQueryHandler>();
        services.AddScoped<IQueryHandler<GetDailyBalanceByDateQuery, DailyBalanceDto?>, GetDailyBalanceByDateQueryHandler>();
        services.AddScoped<IQueryHandler<GetOutboxMessagesQuery, IReadOnlyCollection<OutboxMessageDto>>, GetOutboxMessagesQueryHandler>();
        return services;
    }
}
