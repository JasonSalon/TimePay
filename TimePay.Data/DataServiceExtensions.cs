using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Data.Repositories;

namespace TimePay.Data;

/// <summary>
/// Extension methods for registering TimePay data services.
/// </summary>
public static class DataServiceExtensions
{
    /// <summary>
    /// Adds TimePay database services and all repositories to the DI container.
    /// </summary>
    public static IServiceCollection AddTimePayData(this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<TimePayDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Register repositories
        services.AddScoped<IAuthService, AuthRepository>();
        services.AddScoped<ISettingsService, SettingsRepository>();
        services.AddScoped<ISessionManager, SessionRepository>();
        services.AddScoped<ITransactionService, TransactionRepository>();
        services.AddScoped<IAuditLogger, AuditLogRepository>();

        return services;
    }
}
