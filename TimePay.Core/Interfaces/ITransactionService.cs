using TimePay.Core.Models;

namespace TimePay.Core.Interfaces;

/// <summary>
/// Manages transaction records.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Records a new time-addition transaction.
    /// </summary>
    Task<Transaction> CreateTransactionAsync(Transaction transaction);

    /// <summary>
    /// Gets transaction history with optional filtering.
    /// </summary>
    Task<List<Transaction>> GetTransactionsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, int maxResults = 100);

    /// <summary>
    /// Gets a transaction by its unique identifier.
    /// </summary>
    Task<Transaction?> GetByTransactionIdAsync(string transactionId);
}
