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

namespace TGF.CA.Infrastructure.DB.PostgreSQL {
    internal class PostgreSQLInterceptor(IServiceProvider serviceProvider, IConfiguration configuration)
        : DbConnectionInterceptor {
        private readonly Lazy<Task<string>> _lazyConnectionString = new(() => GetNpgsqlConnectionStringAsync(serviceProvider, configuration), LazyThreadSafetyMode.ExecutionAndPublication);

        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) {
            if (connection is NpgsqlConnection npgsqlConnection)
                npgsqlConnection.ConnectionString = await _lazyConnectionString.Value;
            return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }

        /// <summary>
        /// Gets the PostgreSQL connection string from the configured secrets source.
        /// </summary>
        private static async Task<string> GetNpgsqlConnectionStringAsync(IServiceProvider serviceProvider, IConfiguration configuration) {
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
                    => await ConnectionStringFromSecretsManager(serviceProvider, configuration),

                _ => throw new NotSupportedException($"[ERROR]: Unsupported SecretsSourceType: {configValue}")
            };

            return connectionString + "Pooling=true;MinPoolSize=0;MaxPoolSize=50;";
        }

        private static async Task<string> ConnectionStringFromSecretsManager(IServiceProvider serviceProvider, IConfiguration configuration) {
            var postgreSQLSecrets = await GetPostgreSQLSecrets(serviceProvider);
            var postgreSQLDiscoveryData = await GetPostgreSQLDiscoveryData(serviceProvider);
            var databaseName = PostgreSQLHelpers.GetDatabaseName(configuration);

            return $"Host={postgreSQLDiscoveryData.Server};Port={postgreSQLDiscoveryData.Port};Username={postgreSQLSecrets.Username};Password={postgreSQLSecrets.Password};Database={databaseName};";
        }

        private static async Task<DiscoveryData> GetPostgreSQLDiscoveryData(IServiceProvider serviceProvider) =>
            await serviceProvider.GetRequiredService<IServiceDiscovery>().GetDiscoveryData(InfraServicesRegistry.PostgreSQL)
            ?? throw new Exception("Error retrieving PostgreSQL discovery data!");

        private static async Task<PostgreSQLSecrets> GetPostgreSQLSecrets(IServiceProvider serviceProvider) =>
            await serviceProvider.GetRequiredService<ISecretsManager>().Get<PostgreSQLSecrets>("postgres")
            ?? throw new Exception("Error retrieving PostgreSQL secrets!");

        private record PostgreSQLSecrets : IBasicCredentials {
            public string Username { get; set; } = default!;
            public string Password { get; set; } = default!;
        }
    }
}
