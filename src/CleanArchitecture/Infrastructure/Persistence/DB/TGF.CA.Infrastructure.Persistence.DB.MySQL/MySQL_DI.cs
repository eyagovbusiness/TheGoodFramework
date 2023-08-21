using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.DB.MySQL
{

    /// <summary>
    /// Static lass to support MySQL within the DI framework.
    /// </summary>
    public static class MySQL_DI
    {

        /// <summary>
        /// Adds MySql service with connection to the specified database and using the given DbContext type. Also includes its own healthcheck.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aDatabaseName">Name of the database to connect with.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static async Task<IServiceCollection> AddMySQL<TDbContext>(this IServiceCollection aServiceCollection, string aDatabaseName)
            where TDbContext : DbContext
        {
            var lConnectionString = await MySQLHelpers.GetConnectionString(aServiceCollection.BuildServiceProvider(), aDatabaseName);
            return aServiceCollection
                .AddDbContext<TDbContext>(options => options.UseMySql(lConnectionString, ServerVersion.AutoDetect(lConnectionString)))
                /*.AddMySqlHealthCheckFromConnectionString(lConnectionString+ "SslMode=None;")*/;
        }

        ///// <summary>
        ///// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the MySql database resolved the provided database name and the infrastructure configuration for this web application(using ServiceRegistry and SecretsManager).
        ///// </summary>
        ///// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        ///// <param name="aDatabaseName">strign with MySql database name.</param>
        ///// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        ///// <remarks>Uses Secrets manager to get the sql server username and password and the DiscoveryService to get the host name and port under which the sql server is running.</remarks>
        //public static async Task<IServiceCollection> AddMySqlHealthCheckFromDbName(this IServiceCollection aServiceCollection, string aDatabaseName)
        //    => (await aServiceCollection
        //        .AddHealthChecks()
        //        .AddMySqlHealthCheck(aDatabaseName)
        //        ).Services;

        ///// <summary>
        ///// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the MySql database resolved from the provided aConnectionString.
        ///// </summary>
        ///// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        ///// <param name="aConnectionString">MySql database connection string.</param>
        ///// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        //public static IServiceCollection AddMySqlHealthCheckFromConnectionString(this IServiceCollection aServiceCollection, string aConnectionString)
        //    => aServiceCollection
        //        .AddHealthChecks()
        //        .AddMySql(aConnectionString, "Database")
        //        .Services;

    }
}
