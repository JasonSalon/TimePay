namespace TimePay.Core.Models;

/// <summary>
/// Represents a TimePay administrator account.
/// Passwords are never stored as plaintext — only salted hashes.
/// </summary>
public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded password hash (PBKDF2).
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded salt used for password hashing.
    /// </summary>
    public string PasswordSalt { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
