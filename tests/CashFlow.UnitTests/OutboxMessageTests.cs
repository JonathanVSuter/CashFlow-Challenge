using CashFlow.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CashFlow.UnitTests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Should_Create_With_Pending_Status()
    {
        var outbox = new OutboxMessage("CashEntryCreatedEvent", "{\"entryId\":\"abc\"}");

        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.Attempts.Should().Be(0);
        outbox.PublishedAt.Should().BeNull();
        outbox.Error.Should().BeNull();
        outbox.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Should_Transition_To_Published()
    {
        var outbox = new OutboxMessage("CashEntryCreatedEvent", "{}");
        outbox.MarkAsPublished();

        outbox.Status.Should().Be(OutboxMessageStatus.Published);
        outbox.PublishedAt.Should().NotBeNull();
        outbox.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        outbox.Error.Should().BeNull();
    }

    [Fact]
    public void Should_Increment_Attempts_On_Failure()
    {
        var outbox = new OutboxMessage("CashEntryCreatedEvent", "{}");

        outbox.MarkAsFailed("Connection refused");
        outbox.Attempts.Should().Be(1);
        outbox.Error.Should().Be("Connection refused");
        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public void Should_Transition_To_Failed_After_5_Attempts()
    {
        var outbox = new OutboxMessage("CashEntryCreatedEvent", "{}");

        for (var i = 0; i < 5; i++)
            outbox.MarkAsFailed("Broker unavailable");

        outbox.Status.Should().Be(OutboxMessageStatus.Failed);
        outbox.Attempts.Should().Be(5);
    }

    [Fact]
    public void Should_Remain_Pending_Before_5_Attempts()
    {
        var outbox = new OutboxMessage("CashEntryCreatedEvent", "{}");

        for (var i = 0; i < 4; i++)
            outbox.MarkAsFailed("Broker unavailable");

        outbox.Status.Should().Be(OutboxMessageStatus.Pending);
        outbox.Attempts.Should().Be(4);
    }

    [Fact]
    public void Should_Clear_Error_On_Publish()
    {
        var outbox = new OutboxMessage("CashEntryCreatedEvent", "{}");
        outbox.MarkAsFailed("Some error");
        outbox.MarkAsPublished();

        outbox.Status.Should().Be(OutboxMessageStatus.Published);
        outbox.Error.Should().BeNull();
    }
}
