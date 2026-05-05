using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CashFlow.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer : IHostedService
{
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqTopologyInitializer(RabbitMqConnectionFactory connectionFactory, IOptions<RabbitMqOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_options.QueueName, _options.ExchangeName, _options.RoutingKey);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
