using CashFlow.Application;
using CashFlow.Infrastructure;
using CashFlow.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddCashFlowApplication();
builder.Services.AddCashFlowInfrastructure(builder.Configuration, addOutboxPublisher: false, addRabbitConsumer: true);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await app.RunAsync();
