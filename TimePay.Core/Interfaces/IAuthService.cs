using TimePay.Core.Models;

namespace TimePay.Core.Interfaces;

/// <summary>
/// Handles administrator authentication with secure password hashing.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Creates a new admin user with a securely hashed password.
    /// </summary>
    Task<AdminUser> CreateAdminAsync(string username, string password);

    /// <summary>
    /// Validates admin credentials and returns the admin user if successful, null otherwise.
    /// </summary>
    Task<AdminUser?> ValidateLoginAsync(string username, string password);

    /// <summary>
    /// Changes an admin's password.
    /// </summary>
    Task<bool> ChangePasswordAsync(int adminId, string currentPassword, string newPassword);

    /// <summary>
    /// Checks whether any admin accounts exist (for first-launch setup).
    /// </summary>
    Task<bool> AnyAdminExistsAsync();
}
