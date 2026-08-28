using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {
    /// <summary>
    /// Health check for managed PostgreSQL database using Azure's Managed Identities.
    /// </summary>
    /// <remarks>The valid connection strign with always a valid access token is managed by the interceptor.</remarks>
    internal class PostgreSQLHealthCheck<TDbContext>(TDbContext dbContext)
    : IHealthCheck
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext {

        private const string HEALTH_QUERY = "SELECT 1;";
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            try {
                await dbContext.Database.ExecuteSqlRawAsync(HEALTH_QUERY, cancellationToken);
                return HealthCheckResult.Healthy("PostgreSQL is healthy");
            }
            catch (Exception ex) {
                return HealthCheckResult.Unhealthy("PostgreSQL is unhealthy", ex);
            }
        }
    }
}
