using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.DB.PostgreSQL
{

    /// <summary>
    /// Static lass to support PostgreSQ within the DI framework.
    /// </summary>
    public static class PostgreSQL_DI
    {

        /// <summary>
        /// Adds PostgreSQL service with a connection to the specified database and using the given DbContext type. Also includes its own healthcheck.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <param name="serviceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="databaseName">Name of the database to connect with.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static async Task<IServiceCollection> AddPostgreSQL<TDbContext>(this IServiceCollection aServiceCollection, string aDatabaseName)
            where TDbContext : DbContext
        {
            var lConnectionString = await PostgreSQLHelpers.GetConnectionString(aServiceCollection.BuildServiceProvider(), aDatabaseName);
            return aServiceCollection
                .AddDbContext<TDbContext>(options => options.UseNpgsql(lConnectionString))
                .AddPostgresHealthCheckFromConnectionString(lConnectionString, aDatabasename: aDatabaseName);
        }

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the PostgreSQL database resolved from the provided aConnectionString.
        /// </summary>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aConnectionString">PostgreSQL database connection string.</param>
        /// <param name="aDatabasename">Used to name the healthCheck</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddPostgresHealthCheckFromConnectionString(this IServiceCollection aServiceCollection, string aConnectionString, string? aDatabasename = default)
            => aServiceCollection
                .AddHealthChecks()
                .AddNpgSql(aConnectionString, name: aDatabasename ?? "Database")
                .Services;

    }
}
