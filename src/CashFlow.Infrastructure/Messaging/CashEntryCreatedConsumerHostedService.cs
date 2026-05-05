using System.Text;
using System.Text.Json;
using CashFlow.Domain.Entities;
using CashFlow.Domain.Events;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CashFlow.Infrastructure.Messaging;

public sealed class CashEntryCreatedConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<CashEntryCreatedConsumerHostedService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public CashEntryCreatedConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<CashEntryCreatedConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, durable: true);
        _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_options.QueueName, _options.ExchangeName, _options.RoutingKey);
        _channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<CashEntryCreatedEvent>(json)
                    ?? throw new InvalidOperationException("Invalid message payload.");

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

                var date = DateOnly.FromDateTime(evt.OccurredAt);

                var balance = await db.DailyBalances.FirstOrDefaultAsync(x => x.Date == date, stoppingToken);
                if (balance is null)
                {
                    balance = new DailyBalance(date);
                    db.DailyBalances.Add(balance);
                }

                balance.Apply(evt.Type, evt.Amount);

                cache.Remove($"daily-balance:{date:yyyy-MM-dd}");

                await db.SaveChangesAsync(stoppingToken);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming RabbitMQ message.");
                _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(_options.QueueName, autoAck: false, consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
