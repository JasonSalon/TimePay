namespace TimePay.Core.Models;

/// <summary>
/// Records every time-addition transaction with the rate used at that moment.
/// Changing the rate later must never retroactively modify existing transactions (spec Section 30).
/// </summary>
public class Transaction
{
    public int Id { get; set; }

    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// The session this transaction belongs to.
    /// </summary>
    public int SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>
    /// The admin who performed this transaction.
    /// </summary>
    public int AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }

    /// <summary>
    /// The monetary amount entered.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The rate at the time of this transaction (rate versioning — spec Section 30).
    /// </summary>
    public decimal MinutesPerPeso { get; set; }

    /// <summary>
    /// Calculated minutes added (Amount × MinutesPerPeso).
    /// </summary>
    public decimal MinutesAdded { get; set; }

    /// <summary>
    /// Expiration timestamp before this transaction.
    /// </summary>
    public DateTimeOffset PreviousExpiration { get; set; }

    /// <summary>
    /// Expiration timestamp after this transaction.
    /// </summary>
    public DateTimeOffset NewExpiration { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
