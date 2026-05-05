using System.Text;
using CashFlow.Domain.Entities;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CashFlow.Infrastructure.Messaging;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_options.QueueName, _options.ExchangeName, _options.RoutingKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var messages = await db.OutboxMessages
                .Where(x => x.Status == OutboxMessageStatus.Pending)
                .OrderBy(x => x.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var message in messages)
            {
                try
                {
                    var body = Encoding.UTF8.GetBytes(message.Payload);
                    var props = channel.CreateBasicProperties();
                    props.Persistent = true;
                    props.MessageId = message.Id.ToString();
                    props.Type = message.Type;
                    props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                    channel.BasicPublish(_options.ExchangeName, _options.RoutingKey, props, body);

                    message.MarkAsPublished();
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
                    message.MarkAsFailed(ex.Message);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
