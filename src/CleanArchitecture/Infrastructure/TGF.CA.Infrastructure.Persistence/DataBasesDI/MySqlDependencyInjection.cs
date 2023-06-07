using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TGF.CA.Infrastructure.Persistence.DataBasesSetup;

namespace TGF.CA.Infrastructure.Persistence.DataBasesDI
{
    /// <summary>
    /// Static lass to support MySql within the DI framework.
    /// </summary>
    public static class MySqlDependencyInjection
    {

        /// <summary>
        /// Adds MySql service with connection to the specified database and using the given DbContext type. Also includes its own healthcheck.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aDatabaseName">Name of the database to connect with.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddMySql<T>(this IServiceCollection aServiceCollection, string aDatabaseName)
            where T : DbContext
        {
            return aServiceCollection
                .AddMySqlDbContext<T>(serviceProvider => MySqlSetup.GetConnectionString(serviceProvider, aDatabaseName))
                .AddMySqlHealthCheck(aDatabaseName);
        }

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IHealthChecksBuilder"/> for the MySql database resolved from the IConfiguration and the provided database name..
        /// </summary>
        /// <param name="aHealthChecksBuilder">Target <see cref="IHealthChecksBuilder"/>.</param>
        /// <param name="aDatabaseName">strign with MySql database name.</param>
        /// <returns>Updated <see cref="IHealthChecksBuilder"/>.</returns>
        public static IHealthChecksBuilder AddMySqlHealthCheck(this IHealthChecksBuilder aHealthChecksBuilder, string aDatabaseName)
        {
            ServiceProvider lServiceProvider = aHealthChecksBuilder.Services.BuildServiceProvider();
            string lMySqlConnectionString = MySqlSetup.GetConnectionString(lServiceProvider, aDatabaseName).Result;
            aHealthChecksBuilder.AddMySql(lMySqlConnectionString, "Database");
            return aHealthChecksBuilder;
        }

        /// <summary>
        /// Adds a healthcheck in the target <see cref="IServiceCollection"/> for the MySql database resolved from the aConnectionStringFunc.
        /// </summary>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aDatabaseName">strign with MySql database name.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        private static IServiceCollection AddMySqlHealthCheck(this IServiceCollection aServiceCollection, string aDatabaseName)
            => aServiceCollection.AddHealthChecks().AddMySqlHealthCheck(aDatabaseName).Services;

        /// <summary>
        /// Adds new <see cref="DbContext"/> to the context service provider.
        /// </summary>
        /// <typeparam name="T"><see cref="DbContext"/> to be added to the DI framework.</typeparam>
        /// <param name="aServiceCollection">Target <see cref="IServiceCollection"/>.</param>
        /// <param name="aConnectionStringFunc">Function to resolve the connection strign with MySql database.</param>
        /// <returns>Updated <see cref="IServiceCollection"/>.</returns>
        private static IServiceCollection AddMySqlDbContext<T>(this IServiceCollection aServiceCollection,
            Func<IServiceProvider, Task<string>> aConnectionStringFunc)
                where T : DbContext
        {
            return aServiceCollection.AddDbContext<T>((serviceProvider, builder) =>
            {
                builder.UseMySQL(aConnectionStringFunc.Invoke(serviceProvider).Result);
            });
        }

    }
}
