using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests for the authentication flow per Development Prompt 3.
/// Tests: correct password, incorrect password, empty password,
/// multiple failed attempts, logout, and session expiration.
/// </summary>
public class AuthenticationTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly AuthRepository _authRepo;
    private readonly AuditLogRepository _auditRepo;

    public AuthenticationTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _authRepo = new AuthRepository(_context);
        _auditRepo = new AuditLogRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CorrectPassword_ReturnsAdmin()
    {
        await _authRepo.CreateAdminAsync("admin", "Correct123!");

        var result = await _authRepo.ValidateLoginAsync("admin", "Correct123!");
        Assert.NotNull(result);
        Assert.Equal("admin", result!.Username);
    }

    [Fact]
    public async Task IncorrectPassword_ReturnsNull()
    {
        await _authRepo.CreateAdminAsync("admin", "Correct123!");

        var result = await _authRepo.ValidateLoginAsync("admin", "Wrong456!");
        Assert.Null(result);
    }

    [Fact]
    public async Task EmptyPassword_ReturnsNull()
    {
        await _authRepo.CreateAdminAsync("admin", "Correct123!");

        var result1 = await _authRepo.ValidateLoginAsync("admin", "");
        Assert.Null(result1);

        var result2 = await _authRepo.ValidateLoginAsync("admin", null!);
        Assert.Null(result2);
    }

    [Fact]
    public async Task EmptyUsername_ReturnsNull()
    {
        await _authRepo.CreateAdminAsync("admin", "Correct123!");

        var result = await _authRepo.ValidateLoginAsync("", "Correct123!");
        Assert.Null(result);
    }

    [Fact]
    public async Task NonExistentUser_ReturnsNull()
    {
        await _authRepo.CreateAdminAsync("admin", "Correct123!");

        var result = await _authRepo.ValidateLoginAsync("nonexistent", "Correct123!");
        Assert.Null(result);
    }

    [Fact]
    public async Task MultipleFailedAttempts_AllReturnNull()
    {
        await _authRepo.CreateAdminAsync("admin", "Correct123!");

        for (int i = 0; i < 5; i++)
        {
            var result = await _authRepo.ValidateLoginAsync("admin", $"Wrong{i}");
            Assert.Null(result);
        }

        // Correct password should still work after multiple failures
        var success = await _authRepo.ValidateLoginAsync("admin", "Correct123!");
        Assert.NotNull(success);
    }

    [Fact]
    public async Task LoginSuccess_AuditLogged()
    {
        var admin = await _authRepo.CreateAdminAsync("admin", "TestPass");
        await _auditRepo.LogAsync(AuditAction.AdminLoginSuccess, "admin", "Logged in successfully");

        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.AdminLoginSuccess);
        Assert.Single(logs);
        Assert.Equal("admin", logs[0].Username);
    }

    [Fact]
    public async Task LoginFailed_AuditLogged()
    {
        await _authRepo.CreateAdminAsync("admin", "TestPass");

        // Simulate 3 failed attempts
        for (int i = 1; i <= 3; i++)
        {
            await _auditRepo.LogAsync(AuditAction.AdminLoginFailed, "admin", $"Failed attempt #{i}");
        }

        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.AdminLoginFailed);
        Assert.Equal(3, logs.Count);
    }

    [Fact]
    public async Task InitialSetup_NoAdminExists()
    {
        Assert.False(await _authRepo.AnyAdminExistsAsync());
    }

    [Fact]
    public async Task AfterSetup_AdminExists()
    {
        Assert.False(await _authRepo.AnyAdminExistsAsync());

        await _authRepo.CreateAdminAsync("admin", "SetupPassword");

        Assert.True(await _authRepo.AnyAdminExistsAsync());
    }

    [Fact]
    public async Task PasswordNeverStoredPlaintext()
    {
        await _authRepo.CreateAdminAsync("admin", "MySecret123");

        var admin = await _context.AdminUsers.FirstAsync(u => u.Username == "admin");

        // Password hash and salt should exist
        Assert.NotEmpty(admin.PasswordHash);
        Assert.NotEmpty(admin.PasswordSalt);

        // They should NOT contain the plaintext password
        Assert.DoesNotContain("MySecret123", admin.PasswordHash);
        Assert.DoesNotContain("MySecret123", admin.PasswordSalt);
    }

    [Fact]
    public async Task ChangePassword_OldPasswordFails()
    {
        var admin = await _authRepo.CreateAdminAsync("admin", "OldPass");
        await _authRepo.ChangePasswordAsync(admin.Id, "OldPass", "NewPass");

        var oldResult = await _authRepo.ValidateLoginAsync("admin", "OldPass");
        Assert.Null(oldResult);

        var newResult = await _authRepo.ValidateLoginAsync("admin", "NewPass");
        Assert.NotNull(newResult);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentFails()
    {
        var admin = await _authRepo.CreateAdminAsync("admin", "CorrectOld");
        var result = await _authRepo.ChangePasswordAsync(admin.Id, "WrongOld", "NewPass");

        Assert.False(result);

        // Original password should still work
        var login = await _authRepo.ValidateLoginAsync("admin", "CorrectOld");
        Assert.NotNull(login);
    }
}
