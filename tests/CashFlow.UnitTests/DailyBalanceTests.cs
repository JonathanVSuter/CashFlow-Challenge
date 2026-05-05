using CashFlow.Domain.Entities;
using CashFlow.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CashFlow.UnitTests;

public sealed class DailyBalanceTests
{
    private static readonly DateOnly TestDate = new(2026, 5, 5);

    [Fact]
    public void Should_Start_With_Zero_Balance()
    {
        var balance = new DailyBalance(TestDate);

        balance.TotalCredits.Should().Be(0m);
        balance.TotalDebits.Should().Be(0m);
        balance.Balance.Should().Be(0m);
        balance.Date.Should().Be(TestDate);
    }

    [Fact]
    public void Should_Calculate_Positive_Balance()
    {
        var balance = new DailyBalance(TestDate);
        balance.Apply(EntryType.Credit, 100m);
        balance.Apply(EntryType.Debit, 40m);

        balance.TotalCredits.Should().Be(100m);
        balance.TotalDebits.Should().Be(40m);
        balance.Balance.Should().Be(60m);
    }

    [Fact]
    public void Should_Calculate_Negative_Balance()
    {
        var balance = new DailyBalance(TestDate);
        balance.Apply(EntryType.Credit, 100m);
        balance.Apply(EntryType.Debit, 150m);

        balance.Balance.Should().Be(-50m);
    }

    [Fact]
    public void Should_Accumulate_Multiple_Credits()
    {
        var balance = new DailyBalance(TestDate);
        balance.Apply(EntryType.Credit, 100m);
        balance.Apply(EntryType.Credit, 200m);
        balance.Apply(EntryType.Credit, 300m);

        balance.TotalCredits.Should().Be(600m);
        balance.Balance.Should().Be(600m);
    }

    [Fact]
    public void Should_Accumulate_Multiple_Debits()
    {
        var balance = new DailyBalance(TestDate);
        balance.Apply(EntryType.Debit, 50m);
        balance.Apply(EntryType.Debit, 75m);

        balance.TotalDebits.Should().Be(125m);
        balance.Balance.Should().Be(-125m);
    }

    [Fact]
    public void Should_Reject_Zero_Amount_On_Apply()
    {
        var balance = new DailyBalance(TestDate);
        var act = () => balance.Apply(EntryType.Credit, 0m);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Amount*");
    }

    [Fact]
    public void Should_Reject_Negative_Amount_On_Apply()
    {
        var balance = new DailyBalance(TestDate);
        var act = () => balance.Apply(EntryType.Debit, -10m);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Amount*");
    }

    [Fact]
    public void Should_Update_LastUpdatedAt_On_Apply()
    {
        var balance = new DailyBalance(TestDate);
        var before = balance.LastUpdatedAt;

        balance.Apply(EntryType.Credit, 100m);

        balance.LastUpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Should_Generate_Unique_Id()
    {
        var b1 = new DailyBalance(TestDate);
        var b2 = new DailyBalance(new DateOnly(2026, 5, 6));

        b1.Id.Should().NotBe(b2.Id);
        b1.Id.Should().NotBeEmpty();
    }
}
