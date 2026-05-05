using CashFlow.Application.Features.CashEntries.Commands.CreateCashEntry;
using CashFlow.Domain.Entities;
using CashFlow.Domain.Enums;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CashFlow.UnitTests;

public sealed class CreateCashEntryHandlerTests
{
    private static ApplicationDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Should_Persist_CashEntry_And_OutboxMessage_Atomically()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var handler = new CreateCashEntryCommandHandler(db);
        var command = new CreateCashEntryCommand(EntryType.Credit, 150.75m, "Venda no cartão", DateTime.UtcNow);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert — lançamento salvo
        db.CashEntries.Should().HaveCount(1);
        var savedEntry = await db.CashEntries.FirstAsync();
        savedEntry.Type.Should().Be(EntryType.Credit);
        savedEntry.Amount.Should().Be(150.75m);
        savedEntry.Description.Should().Be("Venda no cartão");

        // Assert — outbox message salvo
        db.OutboxMessages.Should().HaveCount(1);
        var outbox = await db.OutboxMessages.FirstAsync();
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.Type.Should().Be("CashEntryCreatedEvent");
        outbox.Payload.Should().Contain(result.Id.ToString());
    }

    [Fact]
    public async Task Should_Return_Correct_Dto()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var handler = new CreateCashEntryCommandHandler(db);
        var occurredAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc);
        var command = new CreateCashEntryCommand(EntryType.Debit, 200m, "Pagamento aluguel", occurredAt);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Type.Should().Be(EntryType.Debit);
        result.Amount.Should().Be(200m);
        result.Description.Should().Be("Pagamento aluguel");
        result.OccurredAt.Should().Be(occurredAt);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Persist_Multiple_Entries_Independently()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var handler = new CreateCashEntryCommandHandler(db);

        // Act
        await handler.HandleAsync(new CreateCashEntryCommand(EntryType.Credit, 100m, "Venda A", DateTime.UtcNow), CancellationToken.None);
        await handler.HandleAsync(new CreateCashEntryCommand(EntryType.Debit, 50m, "Despesa B", DateTime.UtcNow), CancellationToken.None);
        await handler.HandleAsync(new CreateCashEntryCommand(EntryType.Credit, 200m, "Venda C", DateTime.UtcNow), CancellationToken.None);

        // Assert
        db.CashEntries.Should().HaveCount(3);
        db.OutboxMessages.Should().HaveCount(3);
        db.CashEntries.Count(e => e.Type == EntryType.Credit).Should().Be(2);
        db.CashEntries.Count(e => e.Type == EntryType.Debit).Should().Be(1);
    }

    [Fact]
    public async Task Should_Include_EntryId_In_Outbox_Payload()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var handler = new CreateCashEntryCommandHandler(db);
        var command = new CreateCashEntryCommand(EntryType.Credit, 100m, "Venda", DateTime.UtcNow);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert — payload deve conter o ID do lançamento para o Worker processar
        var outbox = await db.OutboxMessages.FirstAsync();
        outbox.Payload.Should().Contain(result.Id.ToString());
    }
}
