namespace CashFlow.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "cashflow.exchange";
    public string QueueName { get; set; } = "cashflow.cash-entry-created";
    public string RoutingKey { get; set; } = "cash-entry-created";
}
