using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGF.Common.Extensions;

namespace TGF.CA.Infrastructure.DB.PostgreSQL {

    /// <summary>
    /// Static lass to support PostgreSQ within the DI framework.
    /// </summary>
    public static class PostgreSQL_DI {

        /// <summary>
        /// Adds PostgreSQL service with a connection to the specified database and using the given DbContext type. Also includes its own health check.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static async Task<IServiceCollection> AddPostgreSQL<TDbContext>(
            this IServiceCollection aServiceCollection,
            IConfiguration configuration)
            where TDbContext : Microsoft.EntityFrameworkCore.DbContext {
            var lConnectionString = await RetryUtility.ExecuteWithRetryAsync(
                async () => {
                    return await PostgreSQLHelpers.GetConnectionString(aServiceCollection.BuildServiceProvider(), PostgreSQLHelpers.GetDatabaseName(configuration));
                },
                _ => false, // Retry only on exceptions.
                aMaxRetries: 3, // Customize max retries as needed.
                aDelayMilliseconds: 2000, // Customize delay between retries.
                CancellationToken.None // Pass a CancellationToken if needed.
            );

            return aServiceCollection
                .AddDbContext<TDbContext>(options => options.UseNpgsql(lConnectionString))
                .AddPostgresHealthCheckFromConnectionString<TDbContext>(configuration, lConnectionString);
        }

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the PostgreSQL database resolved from the provided aConnectionString.
        /// </summary>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aConnectionString">PostgreSQL database connection string.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddPostgresHealthCheckFromConnectionString<TDbContext>(this IServiceCollection aServiceCollection, IConfiguration configuration, string aConnectionString)
            where TDbContext : Microsoft.EntityFrameworkCore.DbContext
        => aServiceCollection
            .AddHealthChecks()
            .AddNpgSql(aConnectionString, name: PostgreSQLHelpers.GetDatabaseCQRSName<TDbContext>(configuration) + "Database")
            .Services;

    }
}
