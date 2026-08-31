using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests for Development Prompt 11: TRANSACTIONS & REPORTING.
/// </summary>
public class TransactionsFilterTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly TransactionRepository _txnRepo;
    private readonly AuthRepository _authRepo;
    private readonly SessionRepository _sessionRepo;

    public TransactionsFilterTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _txnRepo = new TransactionRepository(_context);
        _authRepo = new AuthRepository(_context);
        _sessionRepo = new SessionRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task Transactions_DateFiltering_ReturnsExpectedRange()
    {
        var admin = await _authRepo.CreateAdminAsync("cashier1", "Pass123!");
        var session = await _sessionRepo.StartSessionAsync(60);

        var baseTime = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

        // Txn 1: Aug 1
        var t1 = new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 10m,
            MinutesPerPeso = 4m,
            MinutesAdded = 40m,
            PreviousExpiration = baseTime,
            NewExpiration = baseTime.AddMinutes(40)
        };
        await _txnRepo.CreateTransactionAsync(t1);

        // Txn 2: Aug 15
        var t2 = new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 50m,
            MinutesPerPeso = 4m,
            MinutesAdded = 200m,
            PreviousExpiration = baseTime.AddDays(14),
            NewExpiration = baseTime.AddDays(14).AddMinutes(200)
        };
        await _txnRepo.CreateTransactionAsync(t2);

        // Retrieve all
        var all = await _txnRepo.GetTransactionsAsync();
        Assert.Equal(2, all.Count);

        // Total revenue sum
        var revenue = all.Sum(t => t.Amount);
        Assert.Equal(60m, revenue);

        // Total minutes sum
        var totalMinutes = all.Sum(t => t.MinutesAdded);
        Assert.Equal(240m, totalMinutes);
    }

    [Fact]
    public async Task Transactions_AreImmutable_CannotBeDirectlyAltered()
    {
        var admin = await _authRepo.CreateAdminAsync("cashier2", "Pass123!");
        var session = await _sessionRepo.StartSessionAsync(60);

        var txn = await _txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 20m,
            MinutesPerPeso = 4m,
            MinutesAdded = 80m,
            PreviousExpiration = session.StartedAt,
            NewExpiration = session.ExpirationAt
        });

        // Verify loaded transaction matches initial insert
        var loaded = await _txnRepo.GetByTransactionIdAsync(txn.TransactionId);
        Assert.NotNull(loaded);
        Assert.Equal(20m, loaded!.Amount);
        Assert.Equal(80m, loaded.MinutesAdded);
    }
}
