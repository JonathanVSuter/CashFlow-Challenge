using CashFlow.Application.Abstractions.Data;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCashFlowInfrastructure(this IServiceCollection services, IConfiguration configuration, bool addOutboxPublisher, bool addRabbitConsumer)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddHostedService<RabbitMqTopologyInitializer>();

        if (addOutboxPublisher)
            services.AddHostedService<OutboxPublisherBackgroundService>();

        if (addRabbitConsumer)
            services.AddHostedService<CashEntryCreatedConsumerHostedService>();

        return services;
    }
}
