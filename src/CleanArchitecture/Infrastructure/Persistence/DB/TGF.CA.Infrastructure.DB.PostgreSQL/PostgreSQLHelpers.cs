using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Application;
using TGF.CA.Domain.ExternalContracts;
using TGF.CA.Infrastructure.Discovery;
using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.DB.DbContext;

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
        => configuration[ConfigurationKeys.DatabaseName] 
            ?? throw new NullReferenceException($"{ConfigurationKeys.DatabaseName} name not configured in appsettings.");

        /// <summary>
        /// Gets the PostgreSQL connection string from the configured PostgreSQL database for this application.
        /// </summary>
        /// <param name="aServiceProvider">Service provider.</param>
        /// <param name="aDatabaseName">Database name.</param>
        /// <returns>The PostgreSQL connection string.</returns>
        internal static async Task<string> GetConnectionString(IServiceProvider aServiceProvider, string aDatabaseName) {
            var lPostgreSQLSecrets = await GetPostgreSQLSecrets(aServiceProvider);
            var lPostgreSQLDiscoveryData = await GetPostgreSQLDiscoveryData(aServiceProvider);
            return $"Host={lPostgreSQLDiscoveryData.Server};Port={lPostgreSQLDiscoveryData.Port};Username={lPostgreSQLSecrets.Username};Password={lPostgreSQLSecrets.Password};Database={aDatabaseName};";

        }

        #region private

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