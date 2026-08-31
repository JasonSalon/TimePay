using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;

namespace TimePay.Data.Repositories;

/// <summary>
/// Manages transaction records. Transactions are immutable once created —
/// changing the rate later never retroactively modifies existing records (spec Section 30).
/// </summary>
public class TransactionRepository : ITransactionService
{
    private readonly TimePayDbContext _context;

    public TransactionRepository(TimePayDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        if (transaction.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(transaction.Amount), "Amount must be greater than 0.");
        if (transaction.MinutesPerPeso <= 0)
            throw new ArgumentOutOfRangeException(nameof(transaction.MinutesPerPeso), "Rate must be greater than 0.");

        // Generate transaction ID
        var count = await _context.Transactions.CountAsync() + 1;
        transaction.TransactionId = $"TXN-{DateTimeOffset.UtcNow:yyyyMMdd}-{count:D5}";
        transaction.MinutesAdded = transaction.Amount * transaction.MinutesPerPeso;
        transaction.CreatedAt = DateTimeOffset.UtcNow;

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    /// <inheritdoc />
    public async Task<List<Transaction>> GetTransactionsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int maxResults = 100)
    {
        var query = _context.Transactions
            .Include(t => t.AdminUser)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(t => t.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxResults)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Transaction?> GetByTransactionIdAsync(string transactionId)
    {
        return await _context.Transactions
            .Include(t => t.AdminUser)
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
    }
}
