using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.DB.DbContext;
using TGF.CA.Infrastructure.Discovery;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Secrets;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {

    /// <summary>
    /// Helper class for PostgreSQL related operations such as generating connection strings and retrieving database names.
    /// </summary>
    internal static class PostgreSQLHelpers {
        /// <summary>
        /// Gets the database name from the configuration, appen.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <param name="configuration"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">When the databae name is not configured in appsettings.</exception>
        internal static string GetDatabaseCQRSName<TDbContext>(IConfiguration configuration)
            where TDbContext : Microsoft.EntityFrameworkCore.DbContext {
            var dbName = GetDatabaseName(configuration);
            return typeof(IReadOnlyDbContext).IsAssignableFrom(typeof(TDbContext))
                ? dbName + "Query"
                : dbName;
        }

        /// <summary>
        /// Gets the database name from the configuration, appen.
        /// </summary>
        /// <exception cref="NullReferenceException">When the databae name is not configured in appsettings.</exception>
        internal static string GetDatabaseName(IConfiguration configuration)
        => configuration[ConfigurationKeys.Database.DatabaseName]
        ?? throw new NullReferenceException($"{ConfigurationKeys.Database.DatabaseName} not configured in appsettings.");

        /// <summary>
        /// Gets the PostgreSQL connection string from the configured PostgreSQL database for this application.
        /// </summary>
        /// <param name="aServiceProvider">Service provider.</param>
        /// <returns>The PostgreSQL connection string.</returns>
        internal static async Task<string> GetConnectionString(IServiceProvider serviceProvider, IConfiguration configuration) {
            var configValue = configuration[ConfigurationKeys.Database.SecretsSourceType]
                ?? throw new NullReferenceException($"{ConfigurationKeys.Database.SecretsSourceType} not configured in appsettings.");

            var secretsSourceType = Enum.TryParse(typeof(SecretsSourceTypeEnum), configValue, false, out var result)
                ? (SecretsSourceTypeEnum)result
                : throw new ArgumentException($"Invalid SecretsSourceType: {configValue}");

            return secretsSourceType switch {
                SecretsSourceTypeEnum.File
                => (await SecretsFiles.GetSecretFromConfigAsync<PostgreSQLConnectionSecret>(configuration, ConfigurationKeys.SecretsFiles.SecretsFileNames.PostgresSecrets))
                .ToConnectionString(),

                SecretsSourceTypeEnum.SecretsManager
                => await ConnectionStringFromSecretsManager(serviceProvider, configuration),


                _ => throw new NotSupportedException("[ERROR] Error building the connection string: The provided value in appsettings of SecretsSourceType in PostgreSQL section is not supported for PostgreSQL.")
            };
        }

        #region private

        private static async Task<string> ConnectionStringFromSecretsManager(IServiceProvider serviceProvider, IConfiguration configuration) {
            var lPostgreSQLSecrets = await GetPostgreSQLSecrets(serviceProvider);
            var lPostgreSQLDiscoveryData = await GetPostgreSQLDiscoveryData(serviceProvider);
            var databasename = GetDatabaseName(configuration);
            return $"Host={lPostgreSQLDiscoveryData.Server};Port={lPostgreSQLDiscoveryData.Port};Username={lPostgreSQLSecrets.Username};Password={lPostgreSQLSecrets.Password};Database={databasename};";

        }

        private static async Task<DiscoveryData> GetPostgreSQLDiscoveryData(IServiceProvider aServiceProvider)
        => await aServiceProvider
        .GetRequiredService<IServiceDiscovery>()!
        .GetDiscoveryData(InfraServicesRegistry.PostgreSQL)
        ?? throw new Exception("Error loading retrieving the PostgreSQL discovery data!!");

        private static async Task<PostgreSQLSecrets> GetPostgreSQLSecrets(IServiceProvider aServiceProvider)
        => await aServiceProvider
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