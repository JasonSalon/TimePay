using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Core.Security;

namespace TimePay.Data.Repositories;

/// <summary>
/// Implements administrator authentication with PBKDF2 password hashing.
/// Never stores or logs plaintext passwords.
/// </summary>
public class AuthRepository : IAuthService
{
    private readonly TimePayDbContext _context;

    public AuthRepository(TimePayDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<AdminUser> CreateAdminAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        // Check for duplicate username
        var exists = await _context.AdminUsers.AnyAsync(u =>
            u.Username.ToLower() == username.ToLower());
        if (exists)
            throw new InvalidOperationException($"Username '{username}' already exists.");

        var (hash, salt) = PasswordHasher.HashPassword(password);

        var admin = new AdminUser
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync();

        return admin;
    }

    /// <inheritdoc />
    public async Task<AdminUser?> ValidateLoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        var admin = await _context.AdminUsers.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == username.ToLower());

        if (admin == null)
            return null;

        if (!PasswordHasher.VerifyPassword(password, admin.PasswordHash, admin.PasswordSalt))
            return null;

        return admin;
    }

    /// <inheritdoc />
    public async Task<bool> ChangePasswordAsync(int adminId, string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
            throw new ArgumentException("New password cannot be empty.", nameof(newPassword));

        var admin = await _context.AdminUsers.FindAsync(adminId);
        if (admin == null)
            return false;

        if (!PasswordHasher.VerifyPassword(currentPassword, admin.PasswordHash, admin.PasswordSalt))
            return false;

        var (hash, salt) = PasswordHasher.HashPassword(newPassword);
        admin.PasswordHash = hash;
        admin.PasswordSalt = salt;
        admin.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AnyAdminExistsAsync()
    {
        return await _context.AdminUsers.AnyAsync();
    }
}
