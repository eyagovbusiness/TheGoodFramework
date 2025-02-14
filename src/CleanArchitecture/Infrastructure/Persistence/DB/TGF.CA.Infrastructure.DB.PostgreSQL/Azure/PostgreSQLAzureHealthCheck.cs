using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace TGF.CA.Infrastructure.DB.PostgreSQL.Azure {
    /// <summary>
    /// Health check for managed PostgreSQL database using Azure's Managed Identities.
    /// </summary>
    /// <remarks>The valid connection strign with always a valid access token is managed by the interceptor.</remarks>
    internal class PostgreSQLAzureHealthCheck(IConfiguration configuration, ManagedIdentitiyPostgreSQLInterceptor interceptor) : IHealthCheck {
        private const string HEALTH_QUERY = "SELECT 1;";
        private NpgsqlDataSource? _dataSource;
        private string? _currentConnectionString;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            try {
                // Get the current valid connection string
                var newConnectionString = await interceptor.GetValidNpgsqlConnectionAsync(configuration, cancellationToken: cancellationToken);

                // If the connection string changed, recreate the data source
                if (_dataSource == null || _currentConnectionString != newConnectionString) {
                    _currentConnectionString = newConnectionString;
                    _dataSource?.Dispose(); // Dispose old data source safely
                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(_currentConnectionString);
                    _dataSource = dataSourceBuilder.Build();
                }

                await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
                await using var command = new NpgsqlCommand(HEALTH_QUERY, connection);
                await command.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy("PostgreSQL is available.");
            }
            catch (Exception ex) {
                return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.", ex);
            }
        }
    }
}
