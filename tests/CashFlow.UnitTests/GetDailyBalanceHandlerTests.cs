using CashFlow.Application.Features.DailyBalances.Queries.GetDailyBalanceByDate;
using CashFlow.Domain.Entities;
using CashFlow.Domain.Enums;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CashFlow.UnitTests;

public sealed class GetDailyBalanceHandlerTests
{
    private static ApplicationDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IMemoryCache CreateCache() =>
        new MemoryCache(Options.Create(new MemoryCacheOptions()));

    [Fact]
    public async Task Should_Return_Null_When_No_Balance_Exists()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        using var cache = CreateCache();
        var handler = new GetDailyBalanceByDateQueryHandler(db, cache);
        var query = new GetDailyBalanceByDateQuery(new DateOnly(2026, 5, 5));

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Return_Correct_Balance_For_Date()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        using var cache = CreateCache();

        var date = new DateOnly(2026, 5, 5);
        var balance = new DailyBalance(date);
        balance.Apply(EntryType.Credit, 500m);
        balance.Apply(EntryType.Debit, 120m);
        db.DailyBalances.Add(balance);
        await db.SaveChangesAsync();

        var handler = new GetDailyBalanceByDateQueryHandler(db, cache);
        var query = new GetDailyBalanceByDateQuery(date);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Date.Should().Be(date);
        result.TotalCredits.Should().Be(500m);
        result.TotalDebits.Should().Be(120m);
        result.Balance.Should().Be(380m);
    }

    [Fact]
    public async Task Should_Cache_Result_On_First_Query()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        using var cache = CreateCache();

        var date = new DateOnly(2026, 5, 5);
        var balance = new DailyBalance(date);
        balance.Apply(EntryType.Credit, 300m);
        db.DailyBalances.Add(balance);
        await db.SaveChangesAsync();

        var handler = new GetDailyBalanceByDateQueryHandler(db, cache);
        var query = new GetDailyBalanceByDateQuery(date);

        // Act — primeira consulta (popula cache)
        var result1 = await handler.HandleAsync(query, CancellationToken.None);

        // Verificar que o cache foi populado
        var cacheKey = $"daily-balance:{date:yyyy-MM-dd}";
        cache.TryGetValue(cacheKey, out object? cached).Should().BeTrue();
        cached.Should().NotBeNull();

        // Resultado deve ser o mesmo
        result1.Should().NotBeNull();
        result1!.Balance.Should().Be(300m);
    }

    [Fact]
    public async Task Should_Return_Not_Null_For_Different_Date()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        using var cache = CreateCache();

        var date1 = new DateOnly(2026, 5, 5);
        var date2 = new DateOnly(2026, 5, 6);

        var balance1 = new DailyBalance(date1);
        balance1.Apply(EntryType.Credit, 100m);
        var balance2 = new DailyBalance(date2);
        balance2.Apply(EntryType.Credit, 200m);

        db.DailyBalances.AddRange(balance1, balance2);
        await db.SaveChangesAsync();

        var handler = new GetDailyBalanceByDateQueryHandler(db, cache);

        // Act
        var result1 = await handler.HandleAsync(new GetDailyBalanceByDateQuery(date1), CancellationToken.None);
        var result2 = await handler.HandleAsync(new GetDailyBalanceByDateQuery(date2), CancellationToken.None);

        // Assert
        result1!.Balance.Should().Be(100m);
        result2!.Balance.Should().Be(200m);
    }
}
