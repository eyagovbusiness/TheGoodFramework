using Microsoft.Extensions.Configuration;
using TGF.CA.Infrastructure.DB.DbContext;
using TGF.CA.Infrastructure.InvariantConstants;

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

    }
}