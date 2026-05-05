using CashFlow.Domain.Entities;
using CashFlow.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CashFlow.UnitTests;

public sealed class CashEntryTests
{
    [Fact]
    public void Should_Create_Valid_Credit_Entry()
    {
        var now = DateTime.UtcNow;
        var entry = new CashEntry(EntryType.Credit, 100m, "Venda no cartão", now);

        entry.Id.Should().NotBeEmpty();
        entry.Type.Should().Be(EntryType.Credit);
        entry.Amount.Should().Be(100m);
        entry.Description.Should().Be("Venda no cartão");
        entry.OccurredAt.Should().Be(now);
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Create_Valid_Debit_Entry()
    {
        var entry = new CashEntry(EntryType.Debit, 50.75m, "Pagamento fornecedor", DateTime.UtcNow);

        entry.Type.Should().Be(EntryType.Debit);
        entry.Amount.Should().Be(50.75m);
    }

    [Fact]
    public void Should_Reject_Zero_Amount()
    {
        var act = () => new CashEntry(EntryType.Credit, 0m, "Venda", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Amount*");
    }

    [Fact]
    public void Should_Reject_Negative_Amount()
    {
        var act = () => new CashEntry(EntryType.Debit, -10m, "Pagamento", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Amount*");
    }

    [Fact]
    public void Should_Reject_Empty_Description()
    {
        var act = () => new CashEntry(EntryType.Credit, 100m, "", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Description*");
    }

    [Fact]
    public void Should_Reject_Whitespace_Description()
    {
        var act = () => new CashEntry(EntryType.Credit, 100m, "   ", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Description*");
    }

    [Fact]
    public void Should_Trim_Description_Whitespace()
    {
        var entry = new CashEntry(EntryType.Credit, 100m, "  Venda  ", DateTime.UtcNow);
        entry.Description.Should().Be("Venda");
    }

    [Fact]
    public void Should_Generate_Unique_Ids()
    {
        var entry1 = new CashEntry(EntryType.Credit, 100m, "Venda A", DateTime.UtcNow);
        var entry2 = new CashEntry(EntryType.Credit, 100m, "Venda B", DateTime.UtcNow);

        entry1.Id.Should().NotBe(entry2.Id);
    }
}
