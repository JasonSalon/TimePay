using System.Security.Cryptography;

namespace TimePay.Core.Security;

/// <summary>
/// Handles password hashing using PBKDF2 with a random salt.
/// Never stores or logs plaintext passwords (spec Section 13).
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 32; // 256 bits
    private const int HashSize = 64; // 512 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    /// <summary>
    /// Hashes a password with a randomly generated salt.
    /// Returns (hash, salt) as Base64-encoded strings.
    /// </summary>
    public static (string Hash, string Salt) HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>
    /// Verifies a password against a stored hash and salt.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        var salt = Convert.FromBase64String(storedSalt);
        var expectedHash = Convert.FromBase64String(storedHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        // Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
