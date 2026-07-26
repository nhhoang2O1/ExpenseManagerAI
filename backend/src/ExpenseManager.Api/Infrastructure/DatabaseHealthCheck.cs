using ExpenseManager.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExpenseManager.Api.Infrastructure;

/// <summary>
/// Lightweight readiness probe that does not require the optional EF health
/// check package. It executes a real database connection check against the
/// configured PostgreSQL context.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL readiness check failed.", exception);
        }
    }
}
