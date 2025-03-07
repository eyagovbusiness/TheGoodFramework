using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;
using TGF.CA.Application;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Secrets;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {
    /// <summary>
    /// Interceptor for PostgreSQL connections that uses Azure's Managed Identity for authentication.
    /// </summary>
    /// <remarks>
    /// Intended to be registered as a singleton service in the DI container. 
    /// The interceptor will replace the connection string with a new one that includes the access token.
    /// The interceptor will cache the access token and only refresh it when it is about to expire.
    /// </remarks>
    internal class PostgreSQLInterceptor(IServiceProvider serviceProvider, IConfiguration configuration)
    : DbConnectionInterceptor {

        private string? _connectionString;

        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) {
            if (connection is NpgsqlConnection npgsqlConnection) {
                if (_connectionString.IsNullOrWhiteSpace())
                    _connectionString = await GetNpgsqlConnectionStringAsync();
                npgsqlConnection.ConnectionString = _connectionString;
            }
            return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }

        /// <summary>
        /// Gets the PostgreSQL connection string from the configured PostgreSQL database for this application.
        /// </summary>
        /// <returns>The PostgreSQL connection string.</returns>
        internal async Task<string> GetNpgsqlConnectionStringAsync() {
            var configValue = configuration[ConfigurationKeys.Database.SecretsSourceType]
                ?? throw new NullReferenceException($"{ConfigurationKeys.Database.SecretsSourceType} not configured in appsettings.");

            var secretsSourceType = Enum.TryParse(typeof(SecretsSourceTypeEnum), configValue, false, out var result)
                ? (SecretsSourceTypeEnum)result
                : throw new ArgumentException($"Invalid SecretsSourceType: {configValue}");

            var connectionString = secretsSourceType switch {
                SecretsSourceTypeEnum.File
                => (await SecretsFiles.GetSecretFromConfigAsync<PostgreSQLConnectionSecret>(configuration, ConfigurationKeys.SecretsFiles.SecretsFileNames.PostgresSecrets))
                .ToConnectionString(),

                SecretsSourceTypeEnum.SecretsManager
                => await ConnectionStringFromSecretsManager(),

                _ => throw new NotSupportedException("[ERROR] Error building the connection string: The provided value in appsettings of SecretsSourceType in PostgreSQL section is not supported for PostgreSQL.")
            };
            return connectionString + "Pooling=true;MinPoolSize=0;MaxPoolSize=50;";//for now we will keep the pooling configuration here, if flexibility is neede din the future it wll be added to IConfiguration
        }

        #region private

        private async Task<string> ConnectionStringFromSecretsManager() {
            var lPostgreSQLSecrets = await GetPostgreSQLSecrets();
            var lPostgreSQLDiscoveryData = await GetPostgreSQLDiscoveryData();
            var databasename = PostgreSQLHelpers.GetDatabaseName(configuration);
            return $"Host={lPostgreSQLDiscoveryData.Server};Port={lPostgreSQLDiscoveryData.Port};Username={lPostgreSQLSecrets.Username};Password={lPostgreSQLSecrets.Password};Database={databasename};";
        }

        private async Task<DiscoveryData> GetPostgreSQLDiscoveryData()
        => await serviceProvider
        .GetRequiredService<IServiceDiscovery>()!
        .GetDiscoveryData(InfraServicesRegistry.PostgreSQL)
        ?? throw new Exception("Error loading retrieving the PostgreSQL discovery data!!");

        private async Task<PostgreSQLSecrets> GetPostgreSQLSecrets()
        => await serviceProvider
        .GetRequiredService<ISecretsManager>()!
        .Get<PostgreSQLSecrets>("postgres")
        ?? throw new Exception("Error loading retrieving the PostgreSQL secrets!!");

        private record PostgreSQLSecrets : IBasicCredentials {
            public string Username { get; set; } = default!;
            public string Password { get; set; } = default!;
        }
        #endregion
    }
}
